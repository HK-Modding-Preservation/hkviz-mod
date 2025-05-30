﻿using MapChanger.MonoBehaviours;
using Modding;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HKViz {
    internal class MapExport : Loggable {


        private static MapExport? _instance;
        public static MapExport Instance {
            get {
                if (_instance != null) return _instance;
                _instance = new MapExport();
                return _instance;
            }
        }

        private string getObjectPath(GameObject go, GameObject relativeToParent) {
            var builder = new StringBuilder();
            var current = go;
            while (current != null && current != relativeToParent) {
                if (go != current) {
                    builder.Insert(0, "/");
                }
                builder.Insert(0, current.name);
                current = current.transform.parent.gameObject;
            }
            return builder.ToString();
        }
        public MapData Export() {
            Log("Started map export. This should run at the beginning of the game, after buying the first map, but before buying the quill");

            var imports = new StringBuilder();

            var rooms = new List<MapRoomData>();

            var gameMapGO = GameObject.Find("Game_Map(Clone)");
            var gameMapPos = gameMapGO.transform.position;

            TextData textDataFromObjects(SetTextMeshProGameText it) {
                var current = it.gameObject;
                while (current != null) {
                    current.SetActive(true);
                    current = current.transform.parent?.gameObject;
                }
                var tmp = it.GetComponent<TMPro.TextMeshPro>();
                tmp.ForceMeshUpdate(true);
                return new TextData(
                    objectPath: getObjectPath(it.gameObject, gameMapGO),
                    convoName: it.convName,
                    sheetName: it.sheetName,
                    position: it.transform.position - gameMapPos,
                    fontSize: tmp.fontSize,
                    fontWeight: tmp.fontWeight,
                    bounds: new ExportBounds(
                        min: it.transform.TransformPoint(tmp.textBounds.min) - gameMapPos,
                        max: it.transform.TransformPoint(tmp.textBounds.max) - gameMapPos
                    ),
                    origColor: tmp.color
                );
            }

            var areaNames = gameMapGO.GetComponentsInChildren<AreaName>(true)
                .Select(areaName => textDataFromObjects(areaName.GetComponent<SetTextMeshProGameText>()))
                .ToList();

            foreach (var m in gameMapGO.GetComponentsInChildren<RoomSprite>(true)) {
                var roomSpriteDefinition = m.Rsd;

                var textDatas = m.gameObject.GetComponentsInChildren<SetTextMeshProGameText>(true)
                    //.Where(it => it.gameObject.name.Contains("Area Name"))
                    .Select(it => textDataFromObjects(it))
                    .ToList();


                //Log(m.Rsd?.MapZone);
                //Log(m.Rsd?.SceneName);
                //Log(m.OrigColor);

                var selfSpriteRenderer = m.GetComponent<SpriteRenderer>();

                //Log(spriteRenderer.bounds);
                //Log(spriteRenderer.sprite.texture.GetPixels32());

                var mapZone = roomSpriteDefinition.MapZone.ToString();
                var sceneName = roomSpriteDefinition.SceneName;
                var gameObjectName = m.name;
                var roughMapRoom = m.GetComponent<RoughMapRoom>();

                // we can not use the generic GetComponent, since the type is internal
                var isAdditionalMap = m.GetComponent("AdditionalMaps.MonoBehaviours.MappedCustomRoom") != null;
                var additionalMapSpriteRenderer = isAdditionalMap ? m.transform.GetChild(0)?.GetComponent<SpriteRenderer>() : null;

                // if there is a cornifer version we can always get the not cornifer version from roughMapRoom.
                Sprite? sprite = additionalMapSpriteRenderer?.sprite ?? roughMapRoom?.fullSprite ?? selfSpriteRenderer?.sprite;
                var spriteInfo = SpriteInfo.fromSprite(sprite, m.gameObject.name);

                if (m.gameObject.name == "Fungus3_48_bot") {
                    // speical case, since sprite name is duplicated in game of 47. 
                    spriteInfo.name = "Fungus3_48_bottom";
                }
                if (m.gameObject.name == "Ruins2_10") {
                    spriteInfo.name = "Ruins2_10";
                }

                var visualSpriteRenderer = additionalMapSpriteRenderer ?? selfSpriteRenderer;



                bool hasCorniferVersion = (roughMapRoom != null &&
                        roughMapRoom.fullSprite.name != null && // addional map mod ignored wrong rough maps
                        roughMapRoom.fullSprite.name != "" && // addional map mod ignored wrong rough maps
                        roughMapRoom.fullSprite.name != selfSpriteRenderer?.sprite?.name); // rough maps accidentally left in the game? using same sprite ignored.

                var roughSprite = hasCorniferVersion ? selfSpriteRenderer?.sprite : null;
                var roughSpriteInfo = roughSprite != null ? SpriteInfo.fromSprite(roughSprite, m.gameObject.name + "_Cornifer") : null;



                //imports.AppendLine($"import {spriteInfo.name.ToLower()} from '../assets/areas/{spriteInfo.name}.png';");
                //if (roughSpriteInfo != null) {
                //    imports.AppendLine($"import {roughSpriteInfo.name.ToLower()} from '../assets/areas/{roughSpriteInfo.name}.png';");
                //}

                rooms.Add(new MapRoomData() {
                    sceneName = sceneName,
                    spriteInfo = spriteInfo,
                    roughSpriteInfo = roughSpriteInfo,
                    gameObjectName = gameObjectName,
                    mapZone = mapZone,
                    origColor = m.OrigColor,
                    visualBounds = ExportBounds.fromBounds(visualSpriteRenderer.bounds, gameMapPos),
                    playerPositionBounds = ExportBounds.fromBounds(selfSpriteRenderer.bounds, gameMapPos),
                    hasRoughVersion = hasCorniferVersion,
                    // sprite = $"IMPORT_STRING_START:{spriteInfo.name.ToLower()}:IMPORT_STRING_END",
                    // spriteRough = roughSpriteInfo != null ? $"IMPORT_STRING_START:{roughSpriteInfo.name?.ToLower()}:IMPORT_STRING_END" : null,
                    sprite = $"{spriteInfo.name}",
                    spriteRough = roughSpriteInfo != null ? $"{roughSpriteInfo.name}" : null,
                    texts = textDatas
                });
            }

            var mapData = new MapData(
                rooms: rooms,
                areaNames: areaNames
            );
            var json = Json.Stringify(mapData);
            //var js = json
            //    .Replace("\"IMPORT_STRING_START:", "")
            //    .Replace(":IMPORT_STRING_END\"", "")
            //;


            using (var mapWriter = new StreamWriter(StoragePaths.GetUserFilePath("map-export.txt"))) {
                mapWriter.Write(json);
            }

            //using (var importWriter = new StreamWriter(GetUserFilePath("map-imports.txt"))) {
            //    importWriter.Write(imports.ToString());
            //}

            Log("Finished map export");
            return mapData;
        }
    }
}
