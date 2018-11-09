#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion
 
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[Desc("This actor explodes when killed and the kill XP goes to the Spawner.")]
	public class SpawnedExplodesInfo : ExplodesInfo
	{
		public override object Create(ActorInitializer init) { return new SpawnedExplodes(this, init.Self); }
	}

	public class SpawnedExplodes : ConditionalTrait<SpawnedExplodesInfo>, INotifyKilled, INotifyDamage, INotifyCreated
	{
		readonly Health health;
		BuildingInfo buildingInfo;

		public SpawnedExplodes(SpawnedExplodesInfo info, Actor self)
			: base(info)
		{
			health = self.Trait<Health>();
		}

		void INotifyCreated.Created(Actor self)
		{
			buildingInfo = self.Info.TraitInfoOrDefault<BuildingInfo>();
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (IsTraitDisabled || !self.IsInWorld)
				return;

			if (self.World.SharedRandom.Next(100) > Info.Chance)
				return;

            if (!Info.DeathTypes.IsEmpty && !e.Damage.DamageTypes.Overlaps(Info.DeathTypes))
                return;

			var weapon = ChooseWeaponForExplosion(self);
			if (weapon == null)
				return;

			if (weapon.Report != null && weapon.Report.Any())
				Game.Sound.Play(SoundType.World, weapon.Report.Random(e.Attacker.World.SharedRandom), self.CenterPosition);

			if (Info.Type == ExplosionType.Footprint && buildingInfo != null)
			{
				var cells = buildingInfo.UnpathableTiles(self.Location);
				foreach (var c in cells)
					weapon.Impact(Target.FromPos(self.World.Map.CenterOfCell(c)), e.Attacker, Enumerable.Empty<int>());

				return;
			}

			// Use .FromPos since this actor is killed. Cannot use Target.FromActor
			weapon.Impact(Target.FromPos(self.CenterPosition), self.Trait<MissileSpawnerSlave>().Master, Enumerable.Empty<int>());
		}

		WeaponInfo ChooseWeaponForExplosion(Actor self)
		{
			var shouldExplode = self.TraitsImplementing<IExplodeModifier>().All(a => a.ShouldExplode(self));
			var useFullExplosion = self.World.SharedRandom.Next(100) <= Info.LoadedChance;
			return (shouldExplode && useFullExplosion) ? Info.WeaponInfo : Info.EmptyWeaponInfo;
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (IsTraitDisabled || !self.IsInWorld)
				return;

			if (Info.DamageThreshold == 0)
				return;

			if (health.HP * 100 < Info.DamageThreshold * health.MaxHP)
				self.World.AddFrameEndTask(w => self.Kill(e.Attacker));
		}
	}
}
