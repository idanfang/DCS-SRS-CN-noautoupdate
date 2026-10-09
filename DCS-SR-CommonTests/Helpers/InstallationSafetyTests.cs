using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Tests;

[TestClass]
public class InstallationSafetyTests
{
    [TestMethod]
    public void InstalledRevisionFollowsManifestAndRejectsCorruption()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(root);
        try
        {
            Assert.AreEqual("v2.4.1.0-cn.1", UpdaterChecker.GetInstalledManualReleaseTag(root));
            var file = System.IO.Path.Combine(root, "release-manifest.json");
            System.IO.File.WriteAllText(file, "{\"tag\":\"v2.4.1.0-cn.2\"}");
            Assert.AreEqual("v2.4.1.0-cn.2", UpdaterChecker.GetInstalledManualReleaseTag(root));
            System.IO.File.WriteAllText(file, "{\"tag\":\"2.4.1.0\"}");
            Assert.IsNull(UpdaterChecker.GetInstalledManualReleaseTag(root));
            System.IO.File.WriteAllText(file, "broken");
            Assert.IsNull(UpdaterChecker.GetInstalledManualReleaseTag(root));
        }
        finally { System.IO.Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ProcessPathsRespectDirectoryBoundary()
    {
        Assert.IsTrue(InstallationSafety.IsInside(@"C:\SRS\Client\SR-ClientRadio.exe", @"c:\srs"));
        Assert.IsFalse(InstallationSafety.IsInside(@"C:\SRS-other\Client\SR-ClientRadio.exe", @"C:\SRS"));
        Assert.IsFalse(InstallationSafety.IsInside(@"C:\SRS\..\other\SR-ClientRadio.exe", @"C:\SRS"));
    }

    [TestMethod]
    public void InstallationCannotDeleteItsOwnPayload()
    {
        Assert.IsFalse(InstallationSafety.IsValidTarget(@"C:\", @"C:\extract"));
        Assert.IsFalse(InstallationSafety.IsValidTarget(@"C:\extract", @"C:\extract"));
        Assert.IsFalse(InstallationSafety.IsValidTarget(@"C:\extract\installed", @"C:\extract"));
        Assert.IsFalse(InstallationSafety.IsValidTarget(@"C:\temp", @"C:\temp\extract"));
        Assert.IsFalse(InstallationSafety.IsValidTarget("", @"C:\extract"));
        Assert.IsTrue(InstallationSafety.IsValidTarget(@"C:\SRS", @"C:\extract"));
    }
}
