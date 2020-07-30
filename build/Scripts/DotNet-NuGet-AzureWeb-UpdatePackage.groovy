// Copyright 2020 New Relic Corporation. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

import hudson.model.*;
import jenkins.model.*;

def currentBuild = Thread.currentThread().executable;
def buildCause = currentBuild.getCause(Cause);
if (!buildCause.class.toString().contains("UpstreamCause"))
    return

def parentJob = Jenkins.instance.items.find{j -> j.name == buildCause.upstreamProject};
def parentBuild = parentJob.builds.find{b -> b.number == buildCause.upstreamBuild.toInteger()};

// Create the variables representing this job/build, push them back into the parent CI job
if (currentBuild.getEnvironment()["Repository"] == "nuget-azure-web-sites.git") {
    def WebX86BuildNumber = new StringParameterValue("WEBX86_BUILD_NUMBER", currentBuild.getEnvironment()["BUILD_NUMBER"]);
    def paramAction = new ParametersAction(WebX86BuildNumber);
    parentBuild.addAction(paramAction);
}
else {
    def WebX64BuildNumber = new StringParameterValue("WEBX64_BUILD_NUMBER", currentBuild.getEnvironment()["BUILD_NUMBER"]);
    def paramAction = new ParametersAction(WebX64BuildNumber);
    parentBuild.addAction(paramAction);
}