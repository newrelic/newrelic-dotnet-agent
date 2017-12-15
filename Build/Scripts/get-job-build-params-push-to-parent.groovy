import hudson.model.*
import jenkins.model.*

// Get the executing build
def currentBuild = Thread.currentThread().executable;
currentBuild.setDescription(String.format("%s - %s", currentBuild.getEnvironment()["GIT_BRANCH"], currentBuild.getEnvironment()["GIT_COMMIT"].substring(0, 10)));

// Get the build cause
def buildCause = build.getCause(hudson.model.Cause);

if (buildCause.getClass() != Cause$UpstreamCause) {
    return;
}

// Get the parent CI job and build
def parentJob = Jenkins.instance.items.find{j -> j.name == buildCause.upstreamProject};
def parentBuild = parentJob.builds.find{b -> b.number == buildCause.upstreamBuild.toInteger()};

// Create the variables representing this job/build, push them back into the parent CI job
def eVarMasterGitBranch = new StringParameterValue("MASTER_GIT_BRANCH", currentBuild.getEnvironment()["GIT_BRANCH"]);
def eVarMasterJobName = new StringParameterValue("MASTER_JOB_NAME", currentBuild.getEnvironment()["JOB_NAME"]);
def paramAction = new ParametersAction(eVarMasterGitBranch, eVarMasterJobName);
parentBuild.addAction(paramAction);