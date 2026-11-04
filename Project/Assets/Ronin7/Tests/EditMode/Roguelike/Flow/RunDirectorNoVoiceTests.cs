using NUnit.Framework;
using Ronin7.Flow;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Roguelike has no NPC dialogue: RunDirector must not own a RunVoice (Kessler/boss speech).</summary>
    public class RunDirectorNoVoiceTests
    {
        [Test]
        public void RunDirector_HasNoRunVoiceField()
        {
            foreach (var f in typeof(RunDirector).GetFields(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic))
                Assert.AreNotEqual(typeof(RunVoice), f.FieldType, f.Name);
        }
    }
}
