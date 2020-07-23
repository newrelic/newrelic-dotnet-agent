# How to create an integration test fixture in the .Net Agent #

The goal of an integration test is to test as much as possible with as little developer interaction as possible. For example, it would be best for each integration test to run their own server rather than having the developer start the server on their machine.

1. Load the FullAgent solution and build the version of the agent you want to test against
2. Load the IntegrationTest solution
3. SKIP step 4 and 5 if you plan to test the agent against an existing application (view the different applications in the Applications folder) 
4. Create a project for your application and name it after the wrapper, feature, system, or whatever it is you're testing
    * Update project name and namespace to include namespace info, e.g. NewRelic.Agent.IntegrationTests.Applications.ServiceStackRedis
	* Update your application’s build configuration settting to ‘All Configurations’ and uncheck ‘prefer 32-bit’
    * Create a class with the same name as your application in the IntegrationTests project, inside the RemoteServiceFixtures folder
5. Create a new RemoteApplicationFixture in NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
	* Give it a name that matches the name of your application with the word Tests appended 
	* Setup your new fixture with the name of your application directory and executable
6. Create a new ClassFixture in NewRelic.Agent.IntegrationTests

## Notes ##
* Disable NCrunch for integration tests. Instead use Test Explorer.
* Manage NuGet packages for the solution, not the project, to make sure that we use the same library version if already used in other projects.
* All our integration tests rely on the full agent running.
* It is important to take into account how long a test runs. If it takes a long time, it might be better to move the test to a “long-time” test group, esp. if it is a unit or integration test.
* You should be able to run each test in parallel. (SEE application launcher in integration tests folder)