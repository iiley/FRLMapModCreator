using FRLMapMod.Editor;
using NUnit.Framework;

// The CloudScript argument for UpdateDraftFiles: LapTiming is sent under the exact key the Azure
// Function reads ("LapTiming") and omitted when not set.
public class UpdateDraftFilesArgumentTests
{
    [Test]
    public void LapTiming_IsSerializedUnderExactKey()
    {
        var dict = new UpdateDraftFilesArgument { LapTiming = true }.ToDictionary("item-1");
        Assert.AreEqual("item-1", dict["Id"]);
        Assert.AreEqual(true, dict["LapTiming"]);

        dict = new UpdateDraftFilesArgument { LapTiming = false }.ToDictionary("item-1");
        Assert.AreEqual(false, dict["LapTiming"]);
    }

    [Test]
    public void LapTiming_OmittedWhenNull()
    {
        var dict = new UpdateDraftFilesArgument().ToDictionary("item-1");
        Assert.IsFalse(dict.ContainsKey("LapTiming"));
    }
}
