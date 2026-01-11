@echo off
cd WorthBoards.Api.Tests
powershell.exe -ExecutionPolicy Bypass -File run-performance-tests.ps1
cd ..
