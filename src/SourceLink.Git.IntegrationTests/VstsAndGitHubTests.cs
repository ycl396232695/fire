﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using TestUtilities;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class VstsAndGitHubTests : DotNetSdkTestBase
    {
        public VstsAndGitHubTests() 
            : base("Microsoft.SourceLink.Vsts.Git", "Microsoft.SourceLink.GitHub")
        {
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void CustomTranslation()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "ssh://test@vs-ssh.visualstudio.com:22/test-org/_ssh/test-%72epo\u1234%24%2572%2F";
            var repoName = "test-repo\u1234%24%2572%2F";

            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
",
                customTargets: @"
<Target Name=""TranslateVstsToGitHub""
        DependsOnTargets=""$(SourceControlManagerUrlTranslationTargets)""
        BeforeTargets=""SourceControlManagerPublishTranslatedUrls"">

    <PropertyGroup>
      <_Pattern>https://([^.]+)[.]visualstudio.com/([^/]+)/_git/([^/]+)</_Pattern>
      <_Replacement>https://github.com/$2/$3</_Replacement>
    </PropertyGroup>

    <PropertyGroup>
      <ScmRepositoryUrl>$([System.Text.RegularExpressions.Regex]::Replace($(ScmRepositoryUrl), $(_Pattern), $(_Replacement)))</ScmRepositoryUrl>
    </PropertyGroup>

    <ItemGroup>
      <SourceRoot Update=""@(SourceRoot)"">
        <ScmRepositoryUrl>$([System.Text.RegularExpressions.Regex]::Replace(%(SourceRoot.ScmRepositoryUrl), $(_Pattern), $(_Replacement)))</ScmRepositoryUrl>
      </SourceRoot>
    </ItemGroup>
  </Target>
",
                targets: new[]
                {
                    "Build", "Pack"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "@(SourceRoot->'%(SourceLinkUrl)')",
                    "$(SourceLink)",
                    "$(PrivateRepositoryUrl)",
                    "$(RepositoryUrl)"
                },
                expectedResults: new[]
                {
                    ProjectSourceRoot,
                    $"https://raw.githubusercontent.com/test-org/{repoName}/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://github.com/test-org/{repoName}",
                    $"https://github.com/test-org/{repoName}",
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://raw.githubusercontent.com/test-org/{repoName}/{commitSha}/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"https://github.com/test-org/{repoName}");
        }
    }
}
