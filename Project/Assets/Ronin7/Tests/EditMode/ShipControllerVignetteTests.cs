using NUnit.Framework;
using Ronin7.Ship;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="ShipController.ShouldDriveVignette"/>, the pure gate for the comfort-vignette
    /// drive in Update(). Before this fix, Update() drove the vignette off `vignette != null` alone,
    /// so <see cref="ShipController.SetComfortVignette"/>(false) — which zeroes intensity once — was
    /// immediately overridden back up by the very next frame.
    /// </summary>
    public class ShipControllerVignetteTests
    {
        [Test]
        public void FlagOnAndVignettePresent_ReturnsTrue()
        {
            Assert.IsTrue(ShipController.ShouldDriveVignette(useFlag: true, hasVignette: true));
        }

        [Test]
        public void FlagOffButVignettePresent_ReturnsFalse()
        {
            // The regression case: SetComfortVignette(false) leaves the rig instance around, but the
            // feature is off, so Update() must not keep driving its intensity.
            Assert.IsFalse(ShipController.ShouldDriveVignette(useFlag: false, hasVignette: true));
        }

        [Test]
        public void FlagOnButNoVignette_ReturnsFalse()
        {
            Assert.IsFalse(ShipController.ShouldDriveVignette(useFlag: true, hasVignette: false));
        }

        [Test]
        public void FlagOffAndNoVignette_ReturnsFalse()
        {
            Assert.IsFalse(ShipController.ShouldDriveVignette(useFlag: false, hasVignette: false));
        }
    }
}
