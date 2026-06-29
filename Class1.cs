using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // TODO clear unused usings
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RshLib
{
    [BepInPlugin("com.rushellxyz.rshlib", "Rsh Lib", "3.0.2")]
    [BepInDependency("KrokoshaCasualtiesMP", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Dictionary<string, RshItem> itemRegistry = new Dictionary<string, RshItem>();
        public static bool krokMpEnabled;

        void Awake()
        {
            krokMpEnabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("KrokoshaCasualtiesMP") && 0 == PlayerPrefs.GetInt("KrokoshaCasualtiesMP_FORCE_DISABLE_MP_MOD");

            UnityEngine.Debug.Log($"[RshLib] RshLib 3.0.2, KrokMP: {krokMpEnabled}");
            if ("7.0.1" != Application.version)
                UnityEngine.Debug.LogError($"[RshLib] ! GAME VERSION MISMATCH, Expected: 7.0.1, Current: {Application.version}, Loading will continue");
            var harmony = new Harmony("com.rushellxyz.rshlib");
            harmony.PatchAll();

            if (!krokMpEnabled)
                return;
            PatchManualy(harmony, "KrokoshaCasualtiesMP.NewCoolerObjectPacketWriteReadSystem", "LoadObjectResource", "LoadObjectResourcePatch");
            PatchManualy(harmony, "KrokoshaCasualtiesMP.Con", "SpawnThingOnPlayer", "ConPatch");
        }

        public static void RegisterItem(string itemId, RshItem rshItem)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new Exception("The id of item you're trying to register is null or empty! Item wasn't registred.");
            if (itemRegistry.ContainsKey(itemId))
                throw new Exception($"Item {itemId} already was registred before! Item wasn't registred.");
            if (null == rshItem.sprite)
                UnityEngine.Debug.LogWarning($"The sprite of item {itemId} is null");
            if (null == rshItem.info)
                UnityEngine.Debug.LogWarning($"The info of item {itemId} is null");
            if (string.IsNullOrEmpty(rshItem.baseItem))
                rshItem.baseItem = "geofruit";
            itemRegistry.Add(itemId, rshItem);
        }

        void PatchManualy(Harmony harmony, string targetClass, string targetMethod, string prefixClass)
        {
            var target = AccessTools.Method(AccessTools.TypeByName(targetClass), targetMethod);
            var prefix = AccessTools.Method(System.Type.GetType($"RshLib.{prefixClass}"), "Prefix");
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        }

        public static string GetMPSavePath()
        {
            if (!string.IsNullOrEmpty(KrokoshaCasualtiesMP.SavesystemPatch.savedatapathreplacement))
                return KrokoshaCasualtiesMP.SavesystemPatch.savedatapathreplacement;
       else if (KrokoshaCasualtiesMP.KrokoshaScavMultiplayer.network_system_is_running && KrokoshaCasualtiesMP.SavesystemPatch.HasMultiplayerSaveFile())
                return KrokoshaCasualtiesMP.SavesystemPatch.mpsavefolder;
       else     return Application.persistentDataPath;
        }
    }

    public class RshItem
    {
        public Sprite sprite;
        public ItemInfo info;
        public Action<GameObject, string> onSpawn;
        public string baseItem;
    }

    [HarmonyPatch(typeof(GlobalDark), "Awake")]
    internal class GlobalDarkPatch
    {
        static void Postfix(GlobalDark __instance)
        {
            // Shout out to jimmy_king for sharing this code
            var betaBuildObj = GameObject.Find("GlobalDark(Clone)/betabuild");
            var betaBuildText = betaBuildObj.GetComponent<TMP_Text>();
            if (!betaBuildText.text.Contains(" modded"))
                betaBuildText.text = betaBuildText.text.Replace("This is a ", "This is a modded ");

            var textColor = betaBuildText.color;
            textColor.a = 0.0227f;
            betaBuildText.color = textColor;
        }
    }

    [HarmonyPatch(typeof(Utils), "Create", new Type[] { typeof(string), typeof(Vector2), typeof(float) })]
    internal class UtilsPatch1
    {
        static bool Prefix(string id, Vector2 pos, float rot, ref GameObject __result)
        {
            string[] args = id.Split("$");
            string itemId = args[0];
            if (Plugin.itemRegistry.TryGetValue(itemId, out RshItem rshItem))
            {
                __result = UnityEngine.Object.Instantiate(Resources.Load(rshItem.baseItem), pos, Quaternion.Euler(0f, 0f, rot)) as GameObject;
                __result.GetComponent<SpriteRenderer>().sprite = rshItem.sprite;
                __result.GetComponent<Item>().id = itemId;
                __result.name = itemId;
                if (null != rshItem.onSpawn)
                {
                    if (1 < args.Length)
                        rshItem.onSpawn(__result, args[1]);
                    else
                        rshItem.onSpawn(__result, "");
                }
            }
            else
            {
                __result = UnityEngine.Object.Instantiate(Resources.Load(id), pos, Quaternion.Euler(0f, 0f, rot)) as GameObject;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Utils), "Create", new Type[] { typeof(string), typeof(Transform) })]
    internal class UtilsPatch2
    {
        static bool Prefix(string id, Transform trans, ref GameObject __result)
        {
            string[] args = id.Split("$");
            string itemId = args[0];
            if (Plugin.itemRegistry.TryGetValue(itemId, out RshItem rshItem))
            {
                __result = UnityEngine.Object.Instantiate(Resources.Load(rshItem.baseItem), trans) as GameObject;
                __result.GetComponent<SpriteRenderer>().sprite = rshItem.sprite;
                __result.GetComponent<Item>().id = itemId;
                __result.name = itemId;
                if (null != rshItem.onSpawn)
                {
                    if (1 < args.Length)
                        rshItem.onSpawn(__result, args[1]);
                    else
                        rshItem.onSpawn(__result, "");
                }
            }
            else
            {
                __result = UnityEngine.Object.Instantiate(Resources.Load(id), trans) as GameObject;;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ConsoleScript), "RegisterSpawnEntities")]
    internal class ConsoleScriptPatch
    {
        static void Postfix(ConsoleScript __instance)
        {
            Command command = ConsoleScript.SearchExact("spawn");
            foreach (var (key, value) in Plugin.itemRegistry)
            {
                if (!command.argAutofill[0].Contains(key))
                    command.argAutofill[0].Add(key);
            }
        }
    }

    [HarmonyPatch(typeof(BuildingEntity), "Update")]
    internal class BuildingEntityPatch
    {
        static bool Prefix(BuildingEntity __instance)
        {
            if ((bool)__instance.rb && !__instance.ignoreBodyOptimize)
            {
                __instance.rb.bodyType = ((!WorldGeneration.world.worldExists || !WorldGeneration.world.GetClosestChunkRenderer(WorldGeneration.world.WorldToBlockPos(__instance.transform.position)).enabled || !(Time.timeScale <= 5f)) ? RigidbodyType2D.Static : RigidbodyType2D.Dynamic);
            }
            if (!(__instance.health < 0.5f))
            {
                return false;
            }
            __instance.TryGetComponent<SpriteRenderer>(out var component);
            if ((bool)component)
            {
                GameObject obj = UnityEngine.Object.Instantiate(Resources.Load("BuildingBreakParticle"), __instance.transform.position, __instance.transform.rotation) as GameObject;
                ParticleSystem.ShapeModule shape = obj.GetComponent<ParticleSystem>().shape;
                shape.texture = component.sprite.texture;
                shape.sprite = component.sprite;
                obj.GetComponent<ParticleSystem>().Play();
            }
            UnityEngine.Object.Instantiate(Resources.Load<GameObject>("DustBig"), __instance.transform.position, Quaternion.identity);
            if (__instance.animal)
            {
                __instance.gameObject.SendMessage("AnimalDeath");
            }
            Sound.Play("footstep/Rock/11", __instance.transform.position);
            // krok mp would destroy freshitmdrop if no ones nearby
            // yet, this code exceutes on host only, so flag would otherwise false if client openned it
            bool flag = Vector2.Distance(__instance.transform.position, PlayerCamera.main.body.transform.position) < 8f || Plugin.krokMpEnabled;
            ItemDrop[] array = __instance.itemsDropOnDestroy;
            foreach (ItemDrop itemDrop in array)
            {
                if (UnityEngine.Random.Range(0f, 1f) < itemDrop.chance * __instance.dropChanceMultiplier)
                {
                    GameObject gameObject = Utils.Create(itemDrop.id, __instance.transform.position, UnityEngine.Random.Range(0f, 360f));
                    gameObject.GetComponent<Rigidbody2D>().velocity = new Vector2(UnityEngine.Random.Range(-7f, 7f), UnityEngine.Random.Range(-7f, 7f));
                    gameObject.GetComponent<Item>().SetCondition(UnityEngine.Random.Range(itemDrop.conditionMin, itemDrop.conditionMax));
                    if (flag)
                    {
                        gameObject.AddComponent<FreshItemDrop>();
                    }
                }
            }
            for (int j = 0; j < __instance.guaranteedDropAmount; j++)
            {
                List<ItemDrop> list = new List<ItemDrop>();
                foreach (string item in ItemLootPool.pool[__instance.itemCategoriesToAdd[UnityEngine.Random.Range(0, __instance.itemCategoriesToAdd.Length)]])
                {
                    list.Add(new ItemDrop
                    {
                        id = item,
                        chance = 1f,
                        conditionMin = 1f,
                        conditionMax = 1f
                    });
                }
                ItemDrop itemDrop2 = list[UnityEngine.Random.Range(0, list.Count)];
                GameObject gameObject2 = Utils.Create(itemDrop2.id, __instance.transform.position, UnityEngine.Random.Range(0f, 360f));
                gameObject2.GetComponent<Rigidbody2D>().velocity = new Vector2(UnityEngine.Random.Range(-7f, 7f), UnityEngine.Random.Range(-7f, 7f));
                gameObject2.GetComponent<Item>().SetCondition(UnityEngine.Random.Range(itemDrop2.conditionMin, itemDrop2.conditionMax));
                if (flag)
                {
                    gameObject2.AddComponent<FreshItemDrop>();
                }
            }
            for (int k = 0; k < __instance.alwaysDrop.Length; k++)
            {
                ItemDrop itemDrop3 = __instance.alwaysDrop[k];
                GameObject gameObject3 = Utils.Create(itemDrop3.id, __instance.transform.position, UnityEngine.Random.Range(0f, 360f));
                gameObject3.GetComponent<Rigidbody2D>().velocity = new Vector2(UnityEngine.Random.Range(-7f, 7f), UnityEngine.Random.Range(-7f, 7f));
                gameObject3.GetComponent<Item>().SetCondition(UnityEngine.Random.Range(itemDrop3.conditionMin, itemDrop3.conditionMax));
                if (flag)
                {
                    gameObject3.AddComponent<FreshItemDrop>();
                }
            }
            UnityEngine.Object.Destroy(__instance.gameObject);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerCamera), "RefreshTraderInventories")]
    internal class PlayerCameraPatch
    {
        static bool Prefix(PlayerCamera __instance)
        {
            if (!__instance.tradeMenu.activeSelf)
            {
                return false;
            }
            __instance.ClearTraderInventories();
            TraderScript.TraderItemPreference traderItemPreference = TraderScript.TraderItemPreference.WantsKeep;
            float num = 0f;
            for (int i = 0; i < __instance.currentTrader.items.Count; i++)
            {
                TraderItem item = __instance.currentTrader.items[i];
                if (item.preference != traderItemPreference || i == 0)
                {
                    traderItemPreference = item.preference;
                    GameObject obj = Utils.Create("Special/TraderInvSplit", __instance.traderInventory);
                    obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f - num);
                    obj.GetComponentInChildren<TextMeshProUGUI>().text = Locale.GetOther(traderItemPreference.ToString().ToLower());
                    obj.GetComponent<UITooltip>().localeName = Locale.GetOther(traderItemPreference.ToString().ToLower() + "tip");
                    obj.transform.GetChild(1).localEulerAngles = new Vector3(0f, 0f, __instance.currentTrader.collapsedCategories.Contains(traderItemPreference) ? (-90f) : 0f);
                    obj.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(delegate
                    {
                        __instance.TraderToggleCategory(item.preference);
                    });
                    num += 50f;
                }
                if (!__instance.currentTrader.collapsedCategories.Contains(traderItemPreference))
                {
                    GameObject gameObject = Utils.Create("Special/TraderItemPanel", __instance.traderInventory);
                    gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f - num);

                    Sprite sprite = null;
                    GameObject obj2 = null;

                    if (!Plugin.itemRegistry.TryGetValue(item.id, out RshItem rshItem))
                    {
                        obj2 = Resources.Load(item.id) as UnityEngine.GameObject;
                        sprite = obj2.GetComponent<SpriteRenderer>().sprite;
                    }
               else {
                        sprite = rshItem.sprite;
                    }// I had replace not null with { }, it wont compile other way
                    if (null != obj2 && obj2.TryGetComponent<WaterContainerItem>(out var component) && (bool)component.fillSprite && Item.GetItem(item.id) is LiquidItemInfo { defaultContents: { } } liquidItemInfo && liquidItemInfo.defaultContents.Count > 0)
                    {
                        gameObject.transform.GetChild(0).GetComponent<Image>().enabled = true;
                        gameObject.transform.GetChild(0).GetComponent<Image>().sprite = component.fillSprite;
                        gameObject.transform.GetChild(0).GetComponent<Image>().color = Liquids.Registry[liquidItemInfo.defaultContents[0].liquidId].color;
                        gameObject.transform.GetChild(0).GetComponent<RectTransform>().sizeDelta = PlayerCamera.ImageSizeDelta(component.fillSprite.texture, 8f, 64f);
                    }
                    else
                    {
                        gameObject.transform.GetChild(0).GetComponent<Image>().enabled = false;
                    }
                    gameObject.transform.GetChild(1).GetComponent<Image>().sprite = sprite;
                    gameObject.transform.GetChild(1).GetComponent<RectTransform>().sizeDelta = PlayerCamera.ImageSizeDelta(sprite.texture, 8f, 64f);
                    gameObject.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = Item.GetItem(item.id).fullName;
                    gameObject.transform.GetChild(3).GetComponent<TextMeshProUGUI>().text = Item.GetItem(item.id).description;
                    gameObject.transform.GetChild(3).GetComponent<UITooltip>().skipLocale = true;
                    gameObject.transform.GetChild(3).GetComponent<UITooltip>().tipName = Item.GetItem(item.id).fullName;
                    gameObject.transform.GetChild(3).GetComponent<UITooltip>().tipDesc = Item.GetItem(item.id).description;
                    gameObject.transform.GetChild(4).GetComponent<TextMeshProUGUI>().text = string.Format("{0}{1}", Locale.GetOther("costs"), __instance.currentTrader.ItemPrice(item));
                    if (__instance.currentTrader.ItemPrice(item) == 0)
                    {
                        gameObject.transform.GetChild(4).GetComponent<TextMeshProUGUI>().text = Locale.GetOther("free");
                    }
                    gameObject.transform.GetChild(5).GetComponent<TextMeshProUGUI>().text = string.Format("{0}{1:0.##}u", Locale.GetOther("weighs"), Item.GetItem(item.id).weight);
                    gameObject.transform.GetChild(6).GetComponent<Button>().onClick.AddListener(delegate
                    {
                        __instance.currentTrader.TryPurchase(item);
                    });
                    gameObject.transform.GetChild(7).GetComponent<Image>().color = TraderScript.PrefToColor(item.preference);
                    num += 120f;
                }
            }
            __instance.traderInventory.GetComponent<RectTransform>().sizeDelta = new Vector2(__instance.traderInventory.GetComponent<RectTransform>().sizeDelta.x, num);
            return false;
        }
    }

    [HarmonyPatch(typeof(CorpseScript), "Start")]
    internal class CorpseScriptPatch
    {
        static bool Prefix(CorpseScript __instance)
        {
            if (!__instance.animalCorpse)
            {
                int num = 0;
                float num2 = UnityEngine.Random.Range(0f, 1f);
                if (num2 > 0.5f)
                {
                    num++;
                }
                if (num2 > 0.85f)
                {
                    num++;
                }
                if (num2 > 0.95f)
                {
                    num++;
                }
                for (int i = 0; i < num; i++)
                {
                    string[] array = ItemLootPool.pool[__instance.categories[UnityEngine.Random.Range(0, __instance.categories.Length)]].ToArray();
                    GameObject obj = Utils.Create(array[UnityEngine.Random.Range(0, array.Length)], __instance.transform.position + new Vector3(UnityEngine.Random.Range(-3f, 3f), 3f), UnityEngine.Random.Range(0f, 360f));
                    obj.GetComponent<Item>().SetCondition(UnityEngine.Random.Range(0f, 1f));
                    obj.GetComponent<SpriteRenderer>().sortingOrder = __instance.GetComponent<SpriteRenderer>().sortingOrder + 1;
                }
            }
            return false;
        }
    }


    [HarmonyPatch(typeof(SaveSystem), "TryLoadGame")]
    [HarmonyAfter("KrokoshaCasualtiesMP")]
    internal class SaveSystemPatch
    {
        static bool Prefix()
        {
            Body body = PlayerCamera.main.body;
            if (!SaveSystem.loadedRun)
            {
                return false;
            }
            if (!SaveSystem.HasSave())
            {
                SaveSystem.loadedRun = false;
                return false;
            }
            string savePath = "";
            if (Plugin.krokMpEnabled)
                savePath = Plugin.GetMPSavePath();
       else     savePath = Application.persistentDataPath;
            JObject jObject;
            savePath = Path.Combine(savePath, "save.sv");
            try
            {
                jObject = JObject.Parse(SaveSystem.Unzip(File.ReadAllBytes(savePath)));
            }
            catch (Exception ex)
            {
                SaveSystem.loadedRun = false;
                ConsoleScript.instance.Alert("A save file exists at \"" + Application.persistentDataPath + "\\save.sv\", but it could not be parsed. The save is invalid or corrupted.\n" + ex.Message + "\n" + ex.StackTrace);
                return false;
            }
            foreach (JProperty item2 in jObject["body"].Children<JProperty>())
            {
                FieldInfo field = typeof(Body).GetField(item2.Name);
                if (field == null)
                {
                    ConsoleScript.instance.Alert("Trying to load body field \"" + item2.Name + "\", but such field does not exist. Loading will continue. Make a bug report to RshLib.");
                    continue;
                }
                try
                {
                    Type fieldType = field.FieldType;
                    field.SetValue(value: (!(fieldType == typeof(Skills))) ? Convert.ChangeType(item2.Value, fieldType) : item2.Value.ToObject<Skills>(), obj: body);
                }
                catch (Exception ex2)
                {
                    ConsoleScript.instance.Alert("Error occured during setting body field \"" + field.Name + "\".\n" + ex2.Message + "\n" + ex2.StackTrace + "\nLoading will continue. Make a bug report to RshLib.");
                }
            }
            JObject[] array = jObject["limbs"].Children<JObject>().ToArray();
            for (int i = 0; i < array.Length; i++)
            {
                foreach (JProperty item3 in array[i].Children<JProperty>())
                {
                    FieldInfo field2 = typeof(Limb).GetField(item3.Name);
                    if (field2 == null)
                    {
                        ConsoleScript.instance.Alert($"Trying to load limb field \"{item3.Name}\" on limb \"{body.limbs[i]}\", but such field does not exist. Loading will continue. Make a bug report to RshLib.");
                        continue;
                    }
                    try
                    {
                        Type fieldType2 = field2.FieldType;
                        object value = Convert.ChangeType(item3.Value, fieldType2);
                        field2.SetValue(body.limbs[i], value);
                    }
                    catch (Exception ex3)
                    {
                        ConsoleScript.instance.Alert($"Error occured during setting \"{body.limbs[i]}\" field \"{field2.Name}\".\n{ex3.Message}\n{ex3.StackTrace}\nLoading will continue. Make a bug report to RshLib.");
                    }
                }
            }
            SavedItem[] array2 = null;
            try
            {
                array2 = jObject["items"].ToObject<SavedItem[]>();
            }
            catch (Exception ex4)
            {
                ConsoleScript.instance.Alert("Error occured during parsing global item data.\n" + ex4.Message + "\n" + ex4.StackTrace + "\nLoading has been cancelled. Make a bug report to RshLib.");
                return false;
            }
            int num = 0;
            SavedItem[] array3 = array2;
            foreach (SavedItem savedItem in array3)
            {
                Item item = null;
                GameObject gameObject = null;
                try
                {
                    gameObject = Utils.Create(savedItem.id, body.transform.position, 0f);
                    item = gameObject.GetComponent<Item>();
                    item.condition = savedItem.condition;
                    item.favourited = savedItem.favourited;
                }
                catch (Exception ex5)
                {
                    ConsoleScript.instance.Alert("Error occured during creating item \"" + savedItem.id + "\".\n" + ex5.Message + "\n" + ex5.StackTrace + "\nLoading will continue. Are you missing a mod?");
                    continue;
                }
                try
                {
                    if (savedItem.slot >= 0)
                    {
                        if (body.HoldingItem(savedItem.slot))
                        {
                            body.GetItem(savedItem.slot).GetComponent<Container>().LoadItem(item);
                        }
                        else
                        {
                            body.PickUpItem(item, savedItem.slot, force: true);
                        }
                    }
                    else if ((bool)body.GetWearableBySlotID(savedItem.wearSlot))
                    {
                        body.GetWearableBySlotID(savedItem.wearSlot).GetComponent<Container>().LoadItem(item);
                    }
                    else
                    {
                        body.WearWearable(item);
                    }
                }
                catch (Exception ex6)
                {
                    ConsoleScript.instance.Alert($"Error occured during picking up item \"{item}\".\n{ex6.Message}\n{ex6.StackTrace}\nLoading will continue. Make a bug report to RshLib.");
                    continue;
                }
                foreach (JProperty item4 in jObject["itemComponents"][num].Children<JProperty>())
                {
                    Type type = Type.GetType(item4.Name);
                    if (type == null)
                    {
                        ConsoleScript.instance.Alert($"Error occured during loading item \"{item}\". \"{item4.Name}\" is not a valid type. Loading will continue. Make a bug report to RshLib.");
                        continue;
                    }
                    Component component = gameObject.GetComponent(type);
                    if (component == null)
                    {
                        ConsoleScript.instance.Alert($"Error occured during loading item \"{item}\". Component for \"{type.Name}\" doesn't exist on object. Loading will continue. Make a bug report to RshLib.");
                        continue;
                    }
                    foreach (JProperty item5 in item4.Value.Children<JProperty>())
                    {
                        FieldInfo field3 = type.GetField(item5.Name);
                        if (field3 == null)
                        {
                            ConsoleScript.instance.Alert($"Error occured during loading item \"{item}\". Field for \"{item5.Name}\" doesn't exist. Loading will continue. Make a bug report to RshLib.");
                            continue;
                        }
                        try
                        {
                            Type fieldType3 = field3.FieldType;
                            object value2 = ((!(fieldType3 == typeof(List<LiquidStack>))) ? item5.Value.ToObject(fieldType3) : item5.Value.ToObject<List<LiquidStack>>());
                            field3.SetValue(component, value2);
                        }
                        catch (Exception ex7)
                        {
                            ConsoleScript.instance.Alert($"Error occured during loading item \"{item}\".\n{ex7.Message}\n{ex7.StackTrace}\nLoading will continue. Make a bug report to RshLib.");
                        }
                    }
                }
                num++;
            }
            try
            {
                foreach (JProperty item6 in jObject["bodyComponents"].Children<JProperty>())
                {
                    Type type2 = Type.GetType(item6.Name);
                    Component obj = body.gameObject.AddComponent(type2);
                    foreach (JProperty item7 in item6.Value.Children<JProperty>())
                    {
                        FieldInfo field4 = type2.GetField(item7.Name);
                        object value3 = Convert.ChangeType(conversionType: field4.FieldType, value: item7.Value);
                        field4.SetValue(obj, value3);
                    }
                }
                for (int k = 0; k < body.limbs.Length; k++)
                {
                    foreach (JProperty item8 in jObject["limbComponents"][k].Children<JProperty>())
                    {
                        Type type3 = Type.GetType(item8.Name);
                        Component obj2 = body.limbs[k].gameObject.AddComponent(type3);
                        foreach (JProperty item9 in item8.Value.Children<JProperty>())
                        {
                            FieldInfo field5 = type3.GetField(item9.Name);
                            object value4 = Convert.ChangeType(conversionType: field5.FieldType, value: item9.Value);
                            field5.SetValue(obj2, value4);
                        }
                    }
                }
            }
            catch (Exception ex8)
            {
                ConsoleScript.instance.Alert("Error occured during applying player components.\n" + ex8.Message + "\n" + ex8.StackTrace + "\nLoading will continue. Make a bug report to RshLib.");
            }
            try
            {
                WorldGeneration.world.biomeDepth = (int)jObject["biome"];
                WorldGeneration.world.totalTraveled = (int)jObject["totalTraveled"];
                WorldGeneration.world.lootRarityMultiplier = (float)jObject["lootRarity"];
                WorldGeneration.world.trapRarityMultiplier = (float)jObject["trapRarity"];
                WorldGeneration.runSettings = SaveSystem.TupleListToDic(jObject["runSettings"].ToObject<List<(string, string)>>());
                PlayerCamera.main.caloriesConsumed = (int)jObject["caloriesConsumed"];
                SaveSystem.savedRunTime = (float)jObject["runTime"];
                WoundView.view.SetCharDetails((int)jObject["cHeight"], (int)jObject["cAge"], (int)jObject["cId"], (int)jObject["cVer"]);
                body.lastHappiness = (from jv in jObject["lastHappiness"].ToArray()
                select (float)jv).ToArray();
                SavedRecipeData savedRecipeData = jObject["savedRecipeData"].ToObject<SavedRecipeData>();
                for (int num2 = 0; num2 < savedRecipeData.saved.Length; num2++)
                {
                    Recipes.recipes[num2].hasMadeBefore = savedRecipeData.saved[num2].Item1;
                    Recipes.recipes[num2].INT = savedRecipeData.saved[num2].Item2;
                }
            }
            catch (Exception ex9)
            {
                ConsoleScript.instance.Alert("Error occured during applying world state.\n" + ex9.Message + "\n" + ex9.StackTrace + "\nMake a bug report to RshLib.");
            }
            File.Delete(Application.persistentDataPath + "\\save.sv");
            return false;
        }
    }

    [HarmonyPatch(typeof(Item), "SetupItems")]
    internal class ItemPatch1
    {
        static void Postfix()
        {
            foreach (KeyValuePair<string, RshItem> info in Plugin.itemRegistry)
            {
                if (null == info.Value.info)
                    continue;
                if (Item.GlobalItems.ContainsKey(info.Key))
                {
                    UnityEngine.Debug.Log($"GlobalItems already contain {info.Key}, so it will be overrided");
                    Item.GlobalItems[info.Key] = info.Value.info;
                }
                else
                    Item.GlobalItems.Add(info.Key, info.Value.info);
            }
            ItemLootPool.InitializePool();
        }
    }

    [HarmonyPatch(typeof(Item), "Stats", MethodType.Getter)]
    internal class ItemPatch2
    {
        static bool Prefix(Item __instance, ref ItemInfo __result)
        {
            __result = Item.GetItem(__instance.id.Split("$")[0]);
            return false;
        }
    }

    [HarmonyPatch(typeof(Recipe), "resultSprite", MethodType.Getter)]
    internal class RecipePatch1
    {
        static bool Prefix(Recipe __instance, ref (Sprite, Color) __result)
        {
            if (Plugin.itemRegistry.TryGetValue(__instance.result.id, out var rshItem))
            {
                __result = (rshItem.sprite, UnityEngine.Color.white);
                return false;
            }
            else
                return true;
        }
    }

    [HarmonyPatch(typeof(Recipe), "fullName", MethodType.Getter)]
    internal class RecipePatch2
    {
        static bool Prefix(Recipe __instance, ref string __result)
        {
            __result = (__instance.hasMadeBefore ? "" : "<sprite index=25>");
            if (__instance.isRepair)
            {
                __result += Locale.GetOther("craftrepair");
            }
            if (__instance.result.isLiquid)
            {
                __result += Locale.GetOther(__instance.result.id);
                __result += $" ({(int)__instance.result.resultCondition}mL)";
            }
            else
            {
                __result += Item.GlobalItems[__instance.result.id.Split("$",2)[0]].fullName; // TODO
                if (__instance.result.resultCondition != 1f)
                {
                    __result += $" ({Mathf.RoundToInt(__instance.result.resultCondition * 100f)}%)";
                }
                if (__instance.result.amount > 1)
                {
                    __result += $" (x{__instance.result.amount})";
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Recipe), "simpleName", MethodType.Getter)]
    internal class RecipePatch3
    {
        static bool Prefix(Recipe __instance, ref string __result)
        {
            __result = "";
            if (__instance.result.isLiquid)
            {
                __result += Locale.GetOther(__instance.result.id);
                __result += $" ({(int)__instance.result.resultCondition}mL)";
            }
            else
            {
                __result += Item.GlobalItems[__instance.result.id.Split("$",2)[0]].fullName;
                if (__instance.result.resultCondition != 1f)
                {
                    __result += $" ({Mathf.RoundToInt(__instance.result.resultCondition * 100f)}%)";
                }
                if (__instance.result.amount > 1)
                {
                    __result += $" (x{__instance.result.amount})";
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Recipe), "description", MethodType.Getter)]
    internal class RecipePatch4
    {
        static bool Prefix(Recipe __instance, ref string __result)
        {
            if (__instance.result.isLiquid)
                __result = Locale.GetOther(__instance.result.id + "dsc");
            else
                __result = Item.GlobalItems[__instance.result.id.Split("$",2)[0]].description;
            return false;
        }
    }

    [HarmonyPatch(typeof(Body), "FindByIdThorough")]
    internal class BodyPatch1
    {
        static bool Prefix(Body __instance, ref bool __result, string id, out Item it)
        {
            it = null;
            foreach (Item item in __instance.GetAllItemsThorough())
            {
                if (item.id.Split("$")[0] == id)
                {
                    it = item;
                    __result = true;
                    return false;
                }
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Body), "GetWearable", new Type[] { typeof(string) })]
    internal class BodyPatch2
    {
        static bool Prefix(Body __instance, ref Item __result, string itemid)
        {
            itemid = itemid.Split("$")[0];
            foreach (Transform item in __instance.LimbByName(Item.GlobalItems[itemid.Split("$",2)[0]].desiredWearLimb).transform)
            {
                if (item.TryGetComponent<Item>(out var component) && component.Stats.wearable && component.id.Split("$")[0] == itemid)
                {
                    __result = component;
                    return false;
                }
            }
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Body), "HasWearable", new Type[] { typeof(string) })]
    internal class BodyPatch3
    {
        static bool Prefix(Body __instance, ref bool __result, string itemid)
        {
            itemid = itemid.Split("$")[0];
            foreach (Transform item in __instance.LimbByName(Item.GlobalItems[itemid].desiredWearLimb).transform)
            {
                if (item.TryGetComponent<Item>(out var component) && component.Stats.wearable && component.id.Split("$")[0] == itemid)
                {
                    __result = true;
                    return false;
                }
            }
            __result = false;
            return false;
        }
    }

    internal class LoadObjectResourcePatch
    {
        static bool Prefix(KrokoshaCasualtiesMP.NewCoolerObjectPacketWriteReadSystem __instance, ref GameObject __result, string resourceid, in Vector2 pos)
        {
            if (!Plugin.itemRegistry.TryGetValue(resourceid.Split("$")[0], out var _))
                return true;
            __result = Utils.Create(resourceid, pos, 0f);
            return false;
        }
    }

    internal class ConPatch
    {
        static bool Prefix(ref GameObject __result, string resourceid, Body body = null, bool give_it_to_em = false, GameObject container = null)
        {
            if (KrokoshaCasualtiesMP.KrokoshaScavMultiplayer.is_dedicated_server && body == null && KrokoshaCasualtiesMP.NetPlayer.BodyToPlayerDict.Count > 0)
            {
                body = KrokoshaCasualtiesMP.NetPlayer.BodyToPlayerDict.First().Key;
            }
            if ((object)body == null)
            {
                body = PlayerCamera.main.body;
            }
            GameObject gameObject = Utils.Create(resourceid, (Vector2)body.transform.position + UnityEngine.Random.insideUnitCircle, 0f);
            if (null == gameObject)
            {
                KrokoshaCasualtiesMP.Plugin.log.LogError("Unknown resource: " + resourceid);
                __result = null;
                return false;
            }
            if (gameObject.TryGetComponent<AmmoScript>(out var component))
            {
                component.rounds = component.maxRounds;
            }
            if (gameObject.TryGetComponent<GunScript>(out var component2))
            {
                component2.roundsInMag = component2.magCapacity;
                if (component2.feedType == GunScript.FeedType.Mag)
                {
                    component2.hasMag = true;
                }
                component2.roundInChamber = GunScript.RoundInChamber.Round;
            }
            if (give_it_to_em && gameObject.TryGetComponent<Item>(out var component3))
            {
                if ((bool)container && container.TryGetComponent<Container>(out var component4))
                {
                    component4.LoadItem(component3);
                }
                else
                {
                    body.AutoPickUpItem(component3);
                }
            }
            __result = gameObject;

            return false;
        }
    }
}
