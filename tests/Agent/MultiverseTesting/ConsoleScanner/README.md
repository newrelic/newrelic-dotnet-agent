## Path to XML files

The default config.yml uses an environment variable to find the path to the XML files on the test system. The defaults expect this will be a location in the repo and not from deployed agent installation.  This will need to be in place before running the tool.

Var: `MVS_XML_PATH`

The path should be as follows:  `<PATH_TO_REPO>\src\Agent\NewRelic\Agent\Extensions\Providers\Wrapper`.  The config.yml does not expect and trailing backslash.

## config.yml

More data to come soon!

### Environment variables

To use environment variables, do the following:  `$${{ MY_ENV_VAR }}`.
This will replace the var with the data from the system, but will throw an exception if nothing is found.

### Built in Vars

- `MVS_XML_PATH` is the path to the XML file location.  This is expected to be in the repo and not to an installation of the agent.

### Reporting

The output file is a yaml file and contains the all the validation results.  A slightly more human readable version will be output to the console.

## How to run

Command:
`.\ConsoleScanner.exe "<PATH_TO_CONFIG>\config.yml" "<PATH_TO_OUTPUT_DIR>\reports.yml"`
