﻿using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using SmashTools;

namespace Vehicles.AI
{
	/// <summary>
	/// Grid related method helpers
	/// </summary>
	public static class GenGridVehicles
	{
		/// <summary>
		/// <paramref name="cell"/> is able to be traversed by <paramref name="vehicleDef"/>
		/// </summary>
		/// <param name="cell"></param>
		/// <param name="vehicleDef"></param>
		/// <param name="map"></param>
		public static bool Walkable(this IntVec3 cell, VehicleDef vehicleDef, Map map)
		{
			return map.GetCachedMapComponent<VehicleMapping>()[vehicleDef].VehiclePathGrid.Walkable(cell);
		}

		/// <summary>
		/// <paramref name="cell"/> is able to be stood on for <paramref name="vehicleDef"/>
		/// </summary>
		/// <param name="cell"></param>
		/// <param name="vehicleDef"></param>
		/// <param name="map"></param>
		/// <returns></returns>
		public static bool Standable(this IntVec3 cell, VehicleDef vehicleDef, Map map)
		{
			if (!map.GetCachedMapComponent<VehicleMapping>()[vehicleDef].VehiclePathGrid.Walkable(cell))
			{
				return false;
			}
			List<Thing> list = map.thingGrid.ThingsListAt(cell);
			foreach (Thing t in list)
			{
				if (t.def.passability != Traversability.Standable)
				{
					return false;
				}
			}
			return true;
		}

		//REDO - implement passability based on vehicleDef's customThingCost list
		/// <summary>
		/// <paramref name="cell"/> is impassable for <paramref name="vehicleDef"/>
		/// </summary>
		/// <param name="cell"></param>
		/// <param name="map"></param>
		public static bool Impassable(IntVec3 cell, Map map, VehicleDef vehicleDef)
		{
			List<Thing> list = map.thingGrid.ThingsListAt(cell);
			foreach (Thing t in list)
			{
				if(t.def.passability is Traversability.Impassable)
				{
					return true;
				}
			}
			return false;
		}
	}
}
