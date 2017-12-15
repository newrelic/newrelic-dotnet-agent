# Benchmarking using NBench

This project holds a number of benchmarking measurements and tests leveraging the NBench framework

For more info, see: https://github.com/petabridge/NBench

## How to Run
1. Build in `Release` mode
2. Navigate to `\Tests\Benchmarking\Benchmarking\bin\Release`
3. Execute: `.\NBench.Runner.exe .\Benchmarking.dll`
    * If you'd like markdown output: `.\NBench.Runner.exe .\Benchmarking.dll output-directory="C:\Perf"`

## Long-term vision
* Extract CompositeTestAgent to shared location / introduce new test agent that can serve purpose
* Execute as a part of CI
    * Shorter-term: optional build gate
    * Longer-term: Fail builds if any Test assertions Fail
    * Output results in way can see over time
* Make so doesn't need to build projects?