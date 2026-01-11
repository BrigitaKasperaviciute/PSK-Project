#!/bin/bash

# Bash script to run tests multiple times and calculate average execution time

ITERATIONS=${1:-20}

echo -e "\033[1;36mRunning tests $ITERATIONS times...\033[0m"
echo ""

total_duration=0
successful_runs=0
failed_runs=0
declare -a durations

for ((i=1; i<=ITERATIONS; i++)); do
    echo -e "\033[1;33mRun $i of $ITERATIONS...\033[0m"

    # Run dotnet test with precise timing
    start_time=$(date +%s%3N)
    output=$(dotnet test --nologo --verbosity quiet 2>&1)
    end_time=$(date +%s%3N)

    # Calculate duration in milliseconds
    duration_ms=$((end_time - start_time))
    duration=$(echo "scale=3; $duration_ms / 1000" | bc)

    # Check if tests passed
    if [[ $output =~ "Passed!"|"Test Run Successful" ]]; then
        durations+=($duration)
        total_duration=$(echo "$total_duration + $duration" | bc)
        ((successful_runs++))

        # Format duration with milliseconds
        if (( $(echo "$duration < 1" | bc -l) )); then
            echo -e "  \033[0;32mDuration: ${duration_ms} ms\033[0m"
        else
            echo -e "  \033[0;32mDuration: ${duration} s (${duration_ms} ms)\033[0m"
        fi
    else
        ((failed_runs++))
        echo -e "  \033[0;31mDuration: ${duration} s - FAILED\033[0m"
    fi

    # Small delay between runs
    sleep 0.1
done

echo ""
echo -e "\033[1;36m================================\033[0m"
echo -e "\033[1;36mTest Execution Summary\033[0m"
echo -e "\033[1;36m================================\033[0m"
echo "Total runs: $ITERATIONS"
echo -e "Successful runs: \033[0;32m$successful_runs\033[0m"
if [ $failed_runs -eq 0 ]; then
    echo -e "Failed runs: \033[0;32m$failed_runs\033[0m"
else
    echo -e "Failed runs: \033[0;31m$failed_runs\033[0m"
fi
echo ""

if [ $successful_runs -gt 0 ]; then
    average_duration=$(echo "scale=3; $total_duration / $successful_runs" | bc)
    average_ms=$(echo "scale=0; $average_duration * 1000" | bc)

    # Calculate min and max
    min_duration=${durations[0]}
    max_duration=${durations[0]}
    for duration in "${durations[@]}"; do
        if (( $(echo "$duration < $min_duration" | bc -l) )); then
            min_duration=$duration
        fi
        if (( $(echo "$duration > $max_duration" | bc -l) )); then
            max_duration=$duration
        fi
    done

    min_ms=$(echo "scale=0; $min_duration * 1000" | bc)
    max_ms=$(echo "scale=0; $max_duration * 1000" | bc)
    total_ms=$(echo "scale=0; $total_duration * 1000" | bc)

    # Calculate standard deviation
    variance=0
    for duration in "${durations[@]}"; do
        diff=$(echo "$duration - $average_duration" | bc)
        variance=$(echo "$variance + ($diff * $diff)" | bc)
    done
    std_dev=$(echo "scale=3; sqrt($variance / $successful_runs)" | bc)
    std_dev_ms=$(echo "scale=0; $std_dev * 1000" | bc)

    echo -e "\033[1;36mExecution Time Statistics:\033[0m"
    echo -e "  \033[1;33mAverage: $average_duration s ($average_ms ms)\033[0m"
    echo -e "  \033[0;32mMinimum: $min_duration s ($min_ms ms)\033[0m"
    echo -e "  \033[0;31mMaximum: $max_duration s ($max_ms ms)\033[0m"
    echo -e "  \033[1;36mStd Dev: $std_dev s ($std_dev_ms ms)\033[0m"
    echo "  Total:   $(echo "scale=3; $total_duration" | bc) s ($total_ms ms)"
fi

echo ""
