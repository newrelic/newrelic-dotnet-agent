NewRelic-Azure-Site-Extension
=============================

This is the development project for a Site Extension to Microsoft Azure Web Sites. 

# Purpose
For the new Azure portal (code-named Ibiza) which has been in preview in 2014, the Azure Web Sites installation
and management workflows will allow for the selection of New Relic to be applied to the Web Site. Rather than
requiring the web site developer to include a New Relic NuGet package in their Visual Studio project,
the new portal will allow seamless deployment of the New Relic .NET agent to the web site.

# How It Works
From the Azure portal, an Azure subscriber can add New Relic to their Web Site. When they do this, 
and after the Web Site has been created, a Setup Part (which is just a UI widget) in the Web Site portal blade (a blade is 
a pane in the Azure portal) will install this Site Extension on the site. 

The install.cmd script in the Content folder will be invoked when the Site Extension is installed, and this 
script will install the .NET agent using the latest deployed NuGet package. The script downloads
and installs the appropriate NuGet package: "NewRelic.Azure.WebSites" for 32-bit web sites, and "NewRelic.Azure.WebSites.x64"
for 64-bit web sites.

# Build Information

The following steps should be taken when revising the Site Extension.

1. Make your changes to the script(s).
2. Edit the NewRelic.Azure.WebSites.nuspec file and bump the version number.
3. Save file changes.
4. Run the BuildPackage.cmd file. This will create a new versioned package with a name like NewRelic.Azure.WebSites.<version>.nupkg.

**The following are the steps and [Hubflow](https://quip.com/YPDnAF7vKVhV) commands to start a "release"**

Once it is determined that the content of the [Develop](https://source.datanerd.us/dotNetAgent/NewRelic-Azure-Site-Extension/tree/develop) branch is in a state where a release is pragmatic we then begin a release.

**Hubflow release commands**

(1) From the root of the repo you enter the following command where the #.#.#.# = the incremented version number (The next version # can always be determined by either pulling or [viewing](https://source.datanerd.us/dotNetAgent/NewRelic-Azure-Site-Extension/releases) the tags

```
git hf release start v.#.#.#.#
```

(2) Next you will need to modify the following file on your local instance- incrementing the version # as described in the Build steps above.

(3) Commit the changes

```
git commit -am"up version"
```

(4) Push the changes

```
git hf push
```

(5) Finish the release where v#.#.# is the version number of the new release

```
git hf release finish v#.#.#.#
```

(6) When prompted - make a comment about why you are doing the release.


# Deploying the Site Extension Changes
1. Go to https://www.siteextensions.net/
2. Log in. Currently Bob Uva, Crystal Poole, Matthew Sneeden and David Ebbo (Microsoft) have administrative rights to our Site Extension.
3. Select 'Upload Site Extensions' in the blue menu bar.
4. Select the 'Choose File' button and browse to the NewRelic.Azure.WebSites.<version>.nupkg file that was created.
5. Click the 'Upload' button.
6. After the file has uploaded, you should be on the 'Verify Details & Submit' page in the developer portal.
7. Visually verify that the Title, Description, Icon URL, Project URL, Authors, Copyright fields contain correct information (this 
information comes from the NewRelic.Azure.WebSites.nuspec file that you edited to bump the version number).
8. Select the Save button. You should see the version you uploaded in the 'Version History' of the Site Extension.

# References
This site extension uses our [x86](https://github.com/newrelic/nuget-azure-web-sites) and [x64](https://github.com/newrelic/nuget-azure-web-sites-x64)
NuGet packages for installing the agent.


