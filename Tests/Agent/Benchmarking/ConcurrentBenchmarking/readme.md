# Concurrent Benchmarking using NBench

This project holds a number of benchmarking measurements and tests for concurrent/multithreaded code leveraging the NBench framework.

For more info, see: https://github.com/petabridge/NBench

*Special Note: these tests need to be ran with the cmd flag: `concurrent=true`*

## How to Run
1. Build in `Release` mode
2. Navigate to `\Tests\Benchmarking\ConcurrentBenchmarking\bin\Release`
3. Execute: `.\NBench.Runner.exe .\ConcurrentBenchmarking.dll concurrent=true`
    * If you'd like markdown output: `.\NBench.Runner.exe .\ConcurrentBenchmarking.dll concurrent=true output-directory="C:\Perf"`
    * If you'd like to run an individual test, include: include="TestName". This means the full command is
    `.\NBench.Runner.exe .\ConcurrentBenchmarking.dll concurrent=true include="ConcurrentBenchmarking.CreateEndTransactionBencharking+LotsOfSqlSegmentsTransactionTimedThroughput"` 

## Long-term vision
* Extract CompositeTestAgent to shared location / introduce new test agent that can serve purpose
* Execute as a part of CI
    * Shorter-term: optional build gate
    * Longer-term: Fail builds if any Test assertions Fail
    * Output results in way can see over time
* Make so doesn't need to build projects?
* Potentially single project down the line if can make more obvious a special flag is needed?
