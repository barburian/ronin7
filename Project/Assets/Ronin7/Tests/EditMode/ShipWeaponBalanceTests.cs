using NUnit.Framework;
using Ronin7.Ship;
using UnityEditor;

namespace Ronin7.Tests.EditMode
{
    public class ShipWeaponBalanceTests
    {
        private const string ShipCannonPath = "Assets/Ronin7/Data/ShipCannon.asset";
        private const string InterceptorPath = "Assets/Ronin7/Data/Interceptor.asset";

        [Test]
        public void PlayerCannon_DoesEnoughDamageToOneShot_Interceptor()
        {
            var cannon = AssetDatabase.LoadAssetAtPath<ShipWeaponDefinition>(ShipCannonPath);
            var interceptor = AssetDatabase.LoadAssetAtPath<EnemyShipDefinition>(InterceptorPath);
            Assert.IsNotNull(cannon, "ShipCannon.asset missing");
            Assert.IsNotNull(interceptor, "Interceptor.asset missing");

            Assert.GreaterOrEqual(cannon.damage, interceptor.maxHealth,
                "Player bolt damage must one-shot the Interceptor (see smoother-fighting pass).");
        }

        [Test]
        public void PlayerCannon_BoltLifetime_IsFifteenSeconds()
        {
            var cannon = AssetDatabase.LoadAssetAtPath<ShipWeaponDefinition>(ShipCannonPath);
            Assert.IsNotNull(cannon, "ShipCannon.asset missing");
            Assert.AreEqual(15f, cannon.projectileLifetime, 0.001f);
        }
    }
}
