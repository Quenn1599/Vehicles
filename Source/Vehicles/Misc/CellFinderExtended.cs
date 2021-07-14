﻿using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using SmashTools;
using Vehicles.AI;

namespace Vehicles
{
	public static class CellFinderExtended
	{
		public static IntVec3 RandomEdgeCell(Rot4 dir, Map map, Predicate<IntVec3> validator, int offset)
		{
			List<IntVec3> cellsToCheck = dir.IsValid ? CellRect.WholeMap(map).ContractedBy(offset).GetEdgeCells(dir).ToList() : CellRect.WholeMap(map).ContractedBy(offset).EdgeCells.ToList();
			for(;;)
			{
				IntVec3 rCell = cellsToCheck.PopRandom();
				if (validator(rCell))
				{
					return rCell;
				}
				if(cellsToCheck.Count <= 0)
				{
					Log.Warning("Failed to find edge cell at " + dir.AsInt);
					break;
				}
			}
			return CellFinder.RandomEdgeCell(map);
		}

		public static IntVec3 RandomCenterCell(Map map, Predicate<IntVec3> validator, bool allowRoofed = false)
		{
			Faction hostFaction = map.ParentFaction ?? Faction.OfPlayer;
			List<Thing> thingsOnMap = map.mapPawns.FreeHumanlikesSpawnedOfFaction(hostFaction).Cast<Thing>().ToList();
			if (hostFaction == Faction.OfPlayer)
			{
				thingsOnMap.AddRange(map.listerBuildings.allBuildingsColonist.Cast<Thing>());
			}
			else
			{
				thingsOnMap.AddRange(from x in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial)
					where x.Faction == hostFaction
					select x);
			}
			float num2 = 65f;
			for (int i = 0; i < 300; i++)
			{
				CellFinder.TryFindRandomCellNear(map.Center, map, 30, validator, out IntVec3 intVec);
				if (validator(intVec) && !intVec.Fogged(map))
				{
					if (allowRoofed || !intVec.Roofed(map))
					{
						num2 -= 0.2f;
						bool flag = false;
						foreach (Thing thing in thingsOnMap)
						{
							if ((intVec - thing.Position).LengthHorizontalSquared < num2 * num2)
							{
								flag = true;
								break;
							}
						}
						if (!flag && map.reachability.CanReachFactionBase(intVec, hostFaction))
						{
							return intVec;
						}
					}
				}
			}
			Log.Warning("Unable to find cell to land in... fetching random cell.");
			return CellFinder.RandomCell(map);
		}

		public static IntVec3 MiddleEdgeCell(Rot4 dir, Map map, Pawn pawn, Predicate<IntVec3> validator)
		{
			List<IntVec3> cellsToCheck = CellRect.WholeMap(map).GetEdgeCells(dir).ToList();
			bool riverSpawn = Find.World.CoastDirectionAt(map.Tile) != dir && !Find.WorldGrid[map.Tile].Rivers.NullOrEmpty();
			int padding = (pawn.def.size.z/2) > 4 ? (pawn.def.size.z/2 + 1) : 4;
			int startIndex = cellsToCheck.Count / 2;

			bool riverSpawnValidator(IntVec3 x) => map.terrainGrid.TerrainAt(x) == TerrainDefOf.WaterMovingChestDeep || map.terrainGrid.TerrainAt(x) == TerrainDefOf.WaterMovingShallow;
			
			for (int j = 0; j < 10000; j++)
			{
				IntVec3 c = pawn.ClampToMap(CellFinder.RandomEdgeCell(dir, map), map, padding);
				List<IntVec3> occupiedCells = pawn.PawnOccupiedCells(c, dir.Opposite);
				
				foreach(IntVec3 cAll in occupiedCells)
				{
					if(VehicleHarmony.debug && cAll != c) GenSpawn.Spawn(ThingDefOf.Beer, cAll, map);
					if(!validator(cAll) || (riverSpawn && !riverSpawnValidator(cAll)))
					{
						goto Block_Skip;
					}
				}
				Debug.Message("Found: " + c);
				return c;
				Block_Skip:;
			}
			Log.Warning("Running secondary spawn cell check for boats");
			int i = 0;
			while (cellsToCheck.Count > 0 && i < cellsToCheck.Count / 2)
			{
				if (i > cellsToCheck.Count)
				{
					Log.Warning("List of Cells almost went out of bounds. Report to Boats mod author - Smash Phil");
					break;
				}
				IntVec3 rCell = pawn.ClampToMap(cellsToCheck[startIndex + i], map, padding);
				Debug.Message("Checking right: " + rCell + " | " + validator(rCell));
				List<IntVec3> occupiedCellsRCell = pawn.PawnOccupiedCells(rCell, dir.Opposite);
				foreach (IntVec3 c in occupiedCellsRCell)
				{
					if (!validator(c))
						goto Block_0;
				}
				return rCell;

				Block_0:;
				IntVec3 lCell = pawn.ClampToMap(cellsToCheck[startIndex - i], map, padding);
				Debug.Message("Checking left: " + lCell + " | " + validator(lCell));
				List<IntVec3> occupiedCellsLCell = pawn.PawnOccupiedCells(lCell, dir.Opposite);
				foreach (IntVec3 c in occupiedCellsLCell)
				{
					if (!validator(c))
						goto Block_1;
				}
				return lCell;

				Block_1:;
				i++;
				Debug.Message("==============");
			}
			Log.Error("Could not find valid edge cell to spawn boats on. This could be due to the Boat being too large to spawn on the coast of a Mountainous Map.");
			return pawn.ClampToMap(CellFinder.RandomEdgeCell(dir, map), map, padding);
		}

		public static bool TryFindRandomReachableCellNear(IntVec3 root, Map map, VehicleDef vehicleDef, float radius, TraverseParms traverseParms, Predicate<IntVec3> validator, Predicate<VehicleRegion> regionValidator, out IntVec3 result,
			int maxRegions = 999999)
		{
			if(map is null)
			{
				Log.ErrorOnce("Tried to find reachable cell using SPExtended in a null map", 61037855);
				result = IntVec3.Invalid;
				return false;
			}
			VehicleRegion region = VehicleGridsUtility.GetRegion(root, map, vehicleDef, RegionType.Set_Passable);
			if(region is null)
			{
				result = IntVec3.Invalid;
				return false;
			}
			Rot4 dir = Find.World.CoastDirectionAt(map.Tile).IsValid ? Find.World.CoastDirectionAt(map.Tile) : !Find.WorldGrid[map.Tile].Rivers.NullOrEmpty() ? Ext_Map.RiverDirection(map) : Rot4.Invalid;
			result = RandomEdgeCell(dir, map, (IntVec3 c) => GenGridVehicles.Standable(c, vehicleDef, map) && !c.Fogged(map), 0);
			return true;
		}

		public static bool TryFindRandomCellInWaterRegion(this VehicleRegion reg, Predicate<IntVec3> validator, out IntVec3 result)
		{
			for (int i = 0; i < 10; i++)
			{
				result = reg.RandomCell;
				if(validator is null || validator(result))
					return true;
			}
			List<IntVec3> workingCells = new List<IntVec3>(reg.Cells);
			workingCells.Shuffle<IntVec3>();
			foreach (IntVec3 c in workingCells)
			{
				result = c;
				if (validator is null || validator(result))
				{
					return true;
				}
			}
			result = reg.RandomCell;
			return false;
		}

		public static IntVec3 RandomClosewalkCellNear(IntVec3 root, Map map, VehicleDef vehicleDef, int radius, Predicate<IntVec3> validator = null)
		{
			if (TryRandomClosewalkCellNear(root, map, vehicleDef, radius, out IntVec3 result, validator))
			{
				return result;
			}
			return root;
		}
		
		public static bool TryRandomClosewalkCellNear(IntVec3 root, Map map, VehicleDef vehicleDef, int radius, out IntVec3 result, Predicate<IntVec3> validator = null)
		{
			return TryFindRandomReachableCellNear(root, map, vehicleDef,(float)radius, TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Deadly, false), validator, null, out result, 999999);
		}

		public static IntVec3 RandomSpawnCellForPawnNear(IntVec3 root, Map map, VehiclePawn vehicle, Predicate<IntVec3> validator, bool waterEntry = false, int firstTryWithRadius = 4)
		{
			if(validator(root) && root.GetFirstPawn(map) is null && vehicle.CellRectStandable(map, root))
			{
				return root;
			}

			IntVec3 result;
			int num = firstTryWithRadius;
			for (int i = 0; i < 3; i++)
			{
				if(waterEntry)
				{
					if(TryFindRandomReachableCellNear(root, map, vehicle.VehicleDef, num, TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Deadly, false), 
						(IntVec3 c) => validator(c) && vehicle.CellRectStandable(map, c) && (root.Fogged(map) || !c.Fogged(map)) && c.GetFirstPawn(map) is null, null, out result, 999999))
					{
						return result;
					}
				}
				else
				{
					if(CellFinder.TryFindRandomReachableCellNear(root, map, num, TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Deadly, false), 
						(IntVec3 c) => validator(c) && vehicle.CellRectStandable(map, c) && (root.Fogged(map) || !c.Fogged(map)) && c.GetFirstPawn(map) is null, null, out result, 999999))
					{
						return result;
					}
				}
				
				num *= 2;
			}
			num = firstTryWithRadius + 1;
			
			while (!TryRandomClosewalkCellNear(root, map, vehicle.VehicleDef, num, out result, null))
			{
				if (num > map.Size.x / 2 && num > map.Size.z / 2)
				{
					return root;
				}
				num *= 2;
			}
			return result;
		}
	}
}
