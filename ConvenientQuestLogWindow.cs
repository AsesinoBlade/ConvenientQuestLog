using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ActionsMod;
using UnityEngine;

using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Questing.Actions;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class ConvenientQuestLogWindow : DaggerfallQuestJournalWindow
    {
        struct resourceStruct
        {
            public string type;
            public int sortID;
            public string newSymbol;
            public string keyName;
            public string regionName;
            public string locationName;
            public string buildingName;
            public int mapId;
            public string displayName;
            public string homeRegionName;
            public string homeTownName;
            public string homeBuildingName;
            public string foeType;
            public int foeSpawnCount;
            public int foeKillCount;
            public string itemName;
            public string longName;
            public int stackCount;
            public int startingTimeInSeconds;
            public int remainingTimeInSeconds;
        };

        static bool selectedQuestDisplayed = false;
        TravelTimeCalculator travelTimeCalculator = new TravelTimeCalculator();
        Message selectedQuestMessage;
        static bool travelOptionsModEnabled = false;
        static bool travelOptionsCautiousTravel = false;
        static bool travelOptionsStopAtInnsTravel = false;
        List<Message> groupedQuestMessages;
        static int defaultMessageCheckValue = -1;
        int currentMessageCheck = defaultMessageCheckValue;
        private Dictionary<string, resourceStruct> resourceTable = new Dictionary<string, resourceStruct>();
        Dictionary<string, string> stringTable = null;

        string untitledQuest = "Untitled Quest";
        string activeQuestToolTipText = "Click on a quest message to go back to Active Quests.";
        string journalToolTipText = "Click on a quest for quest information. Click on a location to travel there.";
        string cancelMainStory = "You cannot cancel main story quests. {0} [{1}]";
        string cancelOneTime = "You cannot cancel one time quests.  {0} [{1}]";
        string areYouSure = "Are you sure you want to cancel {0} [{1}]?";
        string currentLocation = "Current location";
        string travelOptionsDuration = "{0} hours {1} mins travel";
        string travelDuration = "{0} days travel";
        string durationSearchToken = "_ day";
        string day = "day";
        string days = "days";
        string questTimeLimitFormat = "{0} ({1} {2} left)";
        string questTimeLimitExtendedFormat = "{0} ({1} {2} {3:00}:{4:00} left)";

        public ConvenientQuestLogWindow(IUserInterfaceManager uiManager) : base(uiManager)
        {
            LoadText();
        }

 

        protected override void Setup()
        {
            base.Setup();
            questLogLabel.OnMiddleMouseClick += QuestLogLabel_OnMiddleMouseClick;
            Mod travelOptionsMod = ModManager.Instance.GetMod("TravelOptions");

            if (travelOptionsMod != null)
            {
                travelOptionsModEnabled = travelOptionsMod.Enabled;
                var travelOptionsSettings = travelOptionsMod.GetSettings();
                travelOptionsCautiousTravel = travelOptionsSettings.GetBool("CautiousTravel", "PlayerControlledCautiousTravel");
                travelOptionsStopAtInnsTravel = travelOptionsSettings.GetBool("StopAtInnsTravel", "PlayerControlledInnsTravel");
            }
        }

        private void QuestLogLabel_OnMiddleMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            bool enhanced = Input.GetKey(KeyCode.LeftShift);
            bool jumpIntoDebug = Input.GetKey(KeyCode.LeftControl);

            if (DisplayMode != JournalDisplay.ActiveQuests)
            {
                base.HandleClick(position, false);
            }
            else
            {
                if (entryLineMap == null)
                    return;
                int line = (int)position.y / questLogLabel.LineHeight;

                if (line < entryLineMap.Count)
                    selectedEntry = entryLineMap[line];
                else
                    selectedEntry = entryLineMap[entryLineMap.Count - 1];

                //Debug.Log($"Line is: {line} entry: {selectedEntry}");

                if (ConvenientQuestLogWindow.selectedQuestDisplayed)
                {
                    currentMessageIndex = 0;
                    SetTextActiveQuests();
                }
                else
                {
                    //ensure nothing happens when last empty line is clicked
                    if (line + 1 >= entryLineMap.Count)
                        return;
                    if (line == 0 || entryLineMap[line - 1] != selectedEntry)
                    {
                        if (enhanced && jumpIntoDebug)
                        {
                            currentMessageIndex = 0;
                            selectedQuestMessage = groupedQuestMessages[selectedEntry];
                            var questSource = RevealQuestStrings(selectedQuestMessage.ParentQuest);
                            DaggerfallUI.Instance.BookReaderWindow.CreateBook(questSource);
                            DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenBookReaderWindow);
                            return;

                        }
                        if (jumpIntoDebug)
                        {
                            HUDQuestDebugger.ShowThisQuest = groupedQuestMessages[selectedEntry].ParentQuest;
                            Thread.Sleep(500);

                            DaggerfallUI.UIManager.PopWindow();
                            return;
                        }
                        currentMessageIndex = 0;
                        selectedQuestMessage = groupedQuestMessages[selectedEntry];
                        DisplayQuestInfo(selectedQuestMessage.ParentQuest, enhanced);
                    }

                }
            }
        }

        bool IncludesPlaceNpc(Task task)
        {
            return task.Actions.OfType<PlaceNpc>().Any();
        }

        public PlaceNpc GetPlaceNpcAction(Task task)
        {
            return task.Actions.OfType<PlaceNpc>().FirstOrDefault();
        }

        void SaveResourceInfo(Quest quest)
        {
            resourceTable.Clear();
            if (quest.resources == null || quest.resources.Count == 0)
                return;

            foreach (KeyValuePair<string, QuestResource> resource in quest.resources.Where(x => x.Value is Place))
            {
                Place place = (Place)resource.Value;
                var resourceStruct = new resourceStruct()
                {
                    type = "Place",
                    sortID = 2,
                    keyName = $"{place.SiteDetails.buildingName} in {place.SiteDetails.locationName}",
                    newSymbol =  resource.Key,
                    regionName = place.SiteDetails.regionName,
                    locationName = place.SiteDetails.locationName,
                    buildingName = place.SiteDetails.buildingName,
                    mapId = place.SiteDetails.mapId,
                };
                resourceTable.Add(resource.Value.Symbol.Original, resourceStruct);
            }

            foreach (KeyValuePair<string, QuestResource> resource in quest.resources.Where(x => x.Value is Person))
            {
                Person person = (Person)resource.Value;
                var resourceStruct = new resourceStruct()
                {
                    type = "Person",
                    sortID = 1,
                    keyName = person.DisplayName,
                    newSymbol = resource.Key,
                    displayName =  person.DisplayName,
                    homeRegionName = person.HomeRegionName,
                    homeTownName = person.HomeTownName,
                    homeBuildingName = person.HomeBuildingName,
                };
                resourceTable.Add(resource.Value.Symbol.Original, resourceStruct);
            }

            foreach (KeyValuePair<string, QuestResource> resource in quest.resources.Where(x => x.Value is Foe))
            {
                Foe foe = (Foe)resource.Value;
                var resourceStruct = new resourceStruct()
                {
                    type = "Foe",
                    sortID = 3,
                    newSymbol = resource.Key,
                    keyName = foe.FoeType.ToString(),
                    foeType = foe.FoeType.ToString(),
                    foeSpawnCount = foe.SpawnCount,
                    foeKillCount = foe.KillCount,
                };
                resourceTable.Add(resource.Value.Symbol.Original, resourceStruct);
            }

            foreach (KeyValuePair<string, QuestResource> resource in quest.resources.Where(x => x.Value is Item))
            {
                Item item = (Item)resource.Value;
                var resourceStruct = new resourceStruct()
                {
                    type = "Item",
                    sortID = 4,
                    newSymbol = resource.Key,
                    keyName = item.DaggerfallUnityItem.LongName == null ? item.DaggerfallUnityItem.ItemName : item.DaggerfallUnityItem.LongName,
                    itemName = item.DaggerfallUnityItem.ItemName,
                    longName = item.DaggerfallUnityItem.LongName,
                    stackCount = item.DaggerfallUnityItem.stackCount,
                };
                resourceTable.Add(resource.Value.Symbol.Original, resourceStruct);
            }

            foreach (KeyValuePair<string, QuestResource> resource in quest.resources.Where(x => x.Value is Clock))
            {
                Clock clock = (Clock)resource.Value;
                var resourceStruct = new resourceStruct()
                {
                    type = "Clock",
                    sortID = 5,
                    newSymbol = resource.Key,
                    keyName = resource.Key,
                    startingTimeInSeconds = clock.StartingTimeInSeconds,
                    remainingTimeInSeconds = clock.RemainingTimeInSeconds,
                };
                resourceTable.Add(resource.Value.Symbol.Original, resourceStruct);
            }

        }

            public string EnhancedInfo(Quest quest, bool enhanced = true)
        {
            QuestMacroHelper macroHelper = new QuestMacroHelper();
            var questSource =  $"\n\n[/scale=1.5][/center][/color=000000]Resources and/or Tasks[/color=000000]for {quest.DisplayName} : {quest.QuestName}\n";

            SaveResourceInfo(quest);


            if (enhanced && resourceTable != null && resourceTable.Count > 0)
            {

                questSource += "\n[/color=ff0000]Revealed List of Quest Resources:\n";

                var prevType = string.Empty;

                foreach (var resource in resourceTable.OrderBy(x => x.Value.sortID))
                {
                    switch (resource.Value.type)
                    {
                        case "Person":
                            if (resource.Value.type != prevType)
                            {
                                questSource += "\n";
                                prevType = resource.Value.type;
                            }
                            questSource +=
                                $"Person: <{resource.Key}>: {resource.Value.displayName} at {resource.Value.homeRegionName}, {resource.Value.homeTownName}, <{resource.Value.homeBuildingName}>\n\n";
                            break;
                        case "Place":
                            if (resource.Value.type != prevType)
                            {
                                questSource += "\n";
                                prevType = resource.Value.type;
                            }
                            questSource +=
                                $"Place: <{resource.Key}>: {resource.Value.regionName}, {resource.Value.locationName}, {resource.Value.buildingName} , <{resource.Value.mapId}>\n\n";
                            break;
                        case "Foe":
                            if (resource.Value.type != prevType)
                            {
                                questSource += "\n";
                                prevType = resource.Value.type;
                            }
                            questSource +=
                                $"Foe: <{resource.Key}>: <{resource.Value.foeType}> Spawn Count: {resource.Value.foeSpawnCount} Kill Count: {resource.Value.foeKillCount} \n\n";
                            break;
                        case "Item":
                            if (resource.Value.type != prevType)
                            {
                                questSource += "\n";
                                prevType = resource.Value.type;
                            }
                            questSource +=
                                $"Item: <{resource.Key} >: [{resource.Value.keyName}] {resource.Value.stackCount} items \n\n";
                            break;
                        case "Clock":
                            if (resource.Value.type != prevType)
                            {
                                questSource += "\n";
                                prevType = resource.Value.type;
                            }
                            var startingDays = resource.Value.startingTimeInSeconds / 86400;
                            var startingHours = (resource.Value.startingTimeInSeconds - startingDays * 86400) / 3600;
                            var startingMinutes = (resource.Value.startingTimeInSeconds - startingDays * 86400 - startingHours * 3600) / 60;
                            var startingSeconds = (resource.Value.startingTimeInSeconds - startingDays * 86400 - startingHours * 3600) % 60;

                            var remainingDays = resource.Value.remainingTimeInSeconds / 86400;
                            var remainingHours = (resource.Value.remainingTimeInSeconds - remainingDays * 86400) / 3600;
                            var remainingMinutes =
                                (resource.Value.remainingTimeInSeconds - remainingDays * 86400 - remainingHours * 3600) / 60;
                            var remainingSeconds =
                                (resource.Value.remainingTimeInSeconds - remainingDays * 86400 - remainingHours * 3600) % 60;

                            questSource +=
                                $"Clock: <{resource.Key}>:Starting Time: {startingDays} days, {startingHours} hrs, {startingMinutes} min, {startingSeconds} sec; Remaining Time: {remainingDays} days, {remainingHours} hrs, {remainingMinutes} min, {remainingSeconds} sec \n\n";
                            break;
                        default:
                            questSource += "";
                            break;
                    }
                }
            }
            string str = "";
            string tab = "          ";
            var lastResourceReferenced = quest.LastResourceReferenced;
            var lastLocationReferenced = quest.LastPlaceReferenced;

            str += $"\n[/color=00ff00]Task Status\n\n";

            foreach (var task in quest.tasks.Values)
            {
                var taskStr = "";
                if (task.Type == Task.TaskType.Headless)
                    taskStr += $"startup ";
                else if (task.Type == Task.TaskType.PersistUntil)
                {
                    taskStr += $"until_{task.TargetSymbol.Name}";
                }
                else
                    taskStr += task.Symbol.Name;

                taskStr += $"{tab}{tab}";
                taskStr += (task.IsTriggered ? "Active" : "Inactive");
                taskStr += "\n";
                if (enhanced )
                {
                    foreach(var action in task.Actions)
                    {
                        if (action != null)
                            taskStr += $"{tab}{ExpandQuestString(quest, action.DebugSource)}\n";
                    }
                }
                str += $"{taskStr}\n";
            }

            questSource += str;
            return questSource;
        }

        public string ExpandQuestString(Quest parentQuest, string questString)
        {
            // Iterate through each key in the resourceTable
            foreach (var key in resourceTable.Keys)
            {
                // Replace all occurrences of the key in questString with the corresponding value from resourceTable
                questString = questString.Replace(key, $"[<{key}> = {resourceTable[key].keyName}]");
            }

            return questString;
        }


        public string RevealQuestStrings(Quest quest)
        {
            var questSource = quest.questSource;
            if (questSource == null)
                questSource = "";

            questSource += EnhancedInfo(quest);
            return questSource;
        }

        void DisplayQuestInfo(Quest quest, bool enhanced)
        {
            var str = "";
            if (enhanced)
                str += EnhancedInfo(quest);
            else
                str += EnhancedInfo(quest, false);
            DaggerfallUI.Instance.BookReaderWindow.CreateBook(str);
            DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenBookReaderWindow);
        }

        void DisplayQuestInfoBackup(Quest quest, bool enhanced)
        {
            var newTokens = new List<TextFile.Token>();
            string str = "";
            int n = 0;
            int questNumber = 0;
            int messageBoxNumber = 1;
            var lastResourceReferenced = quest.LastResourceReferenced;
            var lastLocationReferenced = quest.LastPlaceReferenced;
            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(uiManager, uiManager.TopWindow);
            DaggerfallMessageBox lastMessagebox = messageBox;
            newTokens.Add(new TextFile.Token()
                { formatting = TextFile.Formatting.TextHighlight, text = $" {quest.DisplayName} : {quest.QuestName}" });
            newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyCenter });
            foreach (var task in quest.tasks.Values)
            {
                if (questNumber++ > 10)
                {
                    if (messageBoxNumber == 1)
                    {
                        messageBox.SetTextTokens(newTokens.ToArray());
                        messageBox.ClickAnywhereToClose = true;
                        messageBox.AllowCancel = true;
                        messageBox.ParentPanel.BackgroundColor = Color.clear;
                    }
                    else
                    {
                        var partialMessageBox = new DaggerfallMessageBox(uiManager, messageBox);
                        partialMessageBox.SetTextTokens(newTokens.ToArray());
                        partialMessageBox.ClickAnywhereToClose = true;
                        partialMessageBox.AllowCancel = true;
                        lastMessagebox.AddNextMessageBox(partialMessageBox);
                        lastMessagebox = partialMessageBox;
                    }
                    newTokens = new List<TextFile.Token>();
                    n = 0;
                    questNumber = 0;
                    newTokens.Add(new TextFile.Token()
                    {
                        formatting = TextFile.Formatting.TextHighlight,
                        text = $" {quest.DisplayName} : {quest.QuestName}"
                    });
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyCenter });

                    messageBoxNumber++;
                }
                if (task.Type == Task.TaskType.Headless)
                    str = $"startup ";
                else if (task.Type == Task.TaskType.PersistUntil)
                {
                    str = $"until_{task.TargetSymbol.Name}" ;
                }
                else
                    str = task.Symbol.Name;
                var textFormat = TextFile.Formatting.TextQuestion;
                
                if (task.IsTriggered)
                    textFormat = TextFile.Formatting.TextAnswer;

                newTokens.Add(new TextFile.Token() { formatting = textFormat, text = $"{str}" });

                newTokens.Add(TextFile.TabToken);
                newTokens.Add(TextFile.TabToken);
                if (enhanced && IncludesPlaceNpc(task))
                {
                    var action = GetPlaceNpcAction(task);
                    if (action != null)
                    {
                        newTokens.Add(new TextFile.Token() { formatting = textFormat, text = action.DebugSource });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                    }
                }
                newTokens.Add(new TextFile.Token() { formatting = textFormat, text = (task.IsTriggered ? "Active" : "Inactive") });
                newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyLeft });
            }
            if (n > 2)
            {
                if (messageBoxNumber == 1)
                {
                    messageBox.SetTextTokens(newTokens.ToArray());
                    messageBox.ClickAnywhereToClose = true;
                    messageBox.AllowCancel = true;
                    messageBox.ParentPanel.BackgroundColor = Color.clear;
                    lastMessagebox = messageBox;
                }
                else
                {
                    var partialMessageBox = new DaggerfallMessageBox(uiManager, messageBox);
                    partialMessageBox.SetTextTokens(newTokens.ToArray());
                    partialMessageBox.ClickAnywhereToClose = true;
                    partialMessageBox.AllowCancel = true;
                    lastMessagebox.AddNextMessageBox(partialMessageBox);
                    lastMessagebox = partialMessageBox;
                }
            }

            // Display Resource information
            if (enhanced)
            {
                if (quest.resources.Count(x => x.Value is Person) > 0)
                {
                    //Display persons
                    newTokens = new List<TextFile.Token>();
                    n = 0;

                    str = "Key";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Name";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Region";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Town";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Building";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyLeft });
                    foreach (KeyValuePair<string, QuestResource> resource in quest.resources.Where(x =>
                                 x.Value is Person))
                    {
                        Person person = (Person)resource.Value;
                        str = resource.Key;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = person.DisplayName;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = person.HomeRegionName;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = person.HomeTownName;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = person.HomeBuildingName;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyLeft });

                    }

                    var resourcePersonBox = new DaggerfallMessageBox(uiManager, messageBox);
                    resourcePersonBox.SetTextTokens(newTokens.ToArray());
                    resourcePersonBox.ClickAnywhereToClose = true;
                    resourcePersonBox.AllowCancel = true;
                    lastMessagebox.AddNextMessageBox(resourcePersonBox);
                    lastMessagebox = resourcePersonBox;
                }

                if (quest.resources.Count(x => x.Value is Place) > 0)
                {
                    //Display places
                    newTokens = new List<TextFile.Token>();
                    n = 0;

                    str = "Key";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Region";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Town";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Building";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyLeft });
                    foreach (KeyValuePair<string, QuestResource> resource in quest.resources.Where(
                                 x => x.Value is Place))
                    {
                        Place place = (Place)resource.Value;
                        str = resource.Key;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = place.SiteDetails.regionName;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = place.SiteDetails.locationName;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = place.SiteDetails.buildingName;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyLeft });

                    }

                    var resourcePlaceBox = new DaggerfallMessageBox(uiManager, messageBox);
                    resourcePlaceBox.SetTextTokens(newTokens.ToArray());
                    resourcePlaceBox.ClickAnywhereToClose = true;
                    resourcePlaceBox.AllowCancel = true;
                    lastMessagebox.AddNextMessageBox(resourcePlaceBox);
                    lastMessagebox = resourcePlaceBox;
                }


                if (quest.resources.Count(x => x.Value is Foe) > 0)
                {
                    //Display Foes
                    newTokens = new List<TextFile.Token>();
                    n = 0;

                    str = "Key";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "FoeType";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Kills";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyLeft });
                    foreach (KeyValuePair<string, QuestResource> resource in quest.resources.Where(x => x.Value is Foe))
                    {
                        var foe = (Foe)resource.Value;
                        str = resource.Key;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = foe.FoeType.ToString();
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = $"{foe.KillCount} of {foe.SpawnCount}";
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyLeft });

                    }

                    var resourceFoeBox = new DaggerfallMessageBox(uiManager, messageBox);
                    resourceFoeBox.SetTextTokens(newTokens.ToArray());
                    resourceFoeBox.ClickAnywhereToClose = true;
                    resourceFoeBox.AllowCancel = true;
                    lastMessagebox.AddNextMessageBox(resourceFoeBox);
                    lastMessagebox = resourceFoeBox;
                }

                if (quest.resources.Count(x => x.Value is Item) > 0)
                {
                    //Display Items
                    newTokens = new List<TextFile.Token>();
                    n = 0;

                    str = "Key";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Item Name";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "ItemGroup";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Use Clicked";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(TextFile.TabToken);
                    newTokens.Add(TextFile.TabToken);
                    str = "Player Dropped";
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                    newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyLeft });
                    foreach (KeyValuePair<string, QuestResource> resource in
                             quest.resources.Where(x => x.Value is Item))
                    {
                        var item = (Item)resource.Value;
                        str = resource.Key;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = item.DaggerfallUnityItem.ItemName;
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = item.DaggerfallUnityItem.ItemGroup.ToString();
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = item.UseClicked.ToString();
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(TextFile.TabToken);
                        newTokens.Add(TextFile.TabToken);
                        str = item.PlayerDropped.ToString();
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.TextQuestion, text = $"{str}" });
                        newTokens.Add(new TextFile.Token() { formatting = TextFile.Formatting.JustifyLeft });

                    }

                    var resourceItemBox = new DaggerfallMessageBox(uiManager, messageBox);
                    resourceItemBox.SetTextTokens(newTokens.ToArray());
                    resourceItemBox.ClickAnywhereToClose = true;
                    resourceItemBox.AllowCancel = true;
                    lastMessagebox.AddNextMessageBox(resourceItemBox);
                    lastMessagebox = resourceItemBox;
                }
            }
            messageBox.Show();
        }

        public override void Update()
        {
            base.Update();
            if (DisplayMode != JournalDisplay.ActiveQuests || currentMessageCheck == currentMessageIndex)
                return;

            currentMessageCheck = currentMessageIndex;
            questLogLabel.Clear();

            if (selectedQuestDisplayed)
            {
                SetTextForSelectedQuest(selectedQuestMessage);
            }
            else
            {
                SetTextActiveQuests();
            }
        }

        public override void OnPush()
        {
            base.OnPush();
            currentMessageCheck = defaultMessageCheckValue;
        }

        public override void OnPop()
        {
            base.OnPop();
            currentMessageCheck = defaultMessageCheckValue;
        }

        string UpdateQuestStatus(Quest quest)
        {
            var str = string.Empty;

            var currentQuest = quest;

            // Set quest name
            if (!string.IsNullOrEmpty(quest.DisplayName))
                str += $"{quest.QuestName} '{quest.DisplayName}' ";
            else
                str += $"{quest.QuestName} ";

            // Set quest UID
            str += $"[UID={quest.UID}]";

 
            // Set task labels
            Quest.TaskState[] states = currentQuest.GetTaskStates();
            for (int i = 0; i < states.Length;  i++)
            {
                if (states[i].type == Task.TaskType.Headless)
                    str += $"startup " + (states[i].set ? " completed " : " active");
                else if (states[i].type == Task.TaskType.PersistUntil)
                {
                    Task task = quest.GetTask(states[i].symbol);
                    str += $"until_{task.TargetSymbol.Name}" + (states[i].set ? " completed " : " active");
                }
                else
                    str += states[i].symbol.Name + (states[i].set ? " completed " : " active");
            }
            /*
            // Set timer status
            QuestResource[] clocks = currentQuest.GetAllResources(typeof(Clock));
            for (int i = 0; i < clocks.Length && i < timerLabelPool.Length; i++)
            {
                timerLabelPool[i].Enabled = true;
            }

 
            // Set running status
            // TODO: Use this line for step-through debugging
            if (!currentQuest.QuestComplete)
            {
                processLabel.Text = string.Format("[{0}] - {1}", DaggerfallUnity.Instance.WorldTime.Now.MinTimeString(), questRunning);
            }
            else
            {
                if (currentQuest.QuestSuccess)
                    processLabel.Text = string.Format("[{0}] - {1}", DaggerfallUnity.Instance.WorldTime.Now.MinTimeString(), questFinishedSuccess);
                else
                    processLabel.Text = string.Format("[{0}] - {1}", DaggerfallUnity.Instance.WorldTime.Now.MinTimeString(), questFinishedEnded);
            }
*/
            return str;
        }

        protected override void HandleClick(Vector2 position, bool remove = false)
        {
            if (DisplayMode != JournalDisplay.ActiveQuests)
            {
                base.HandleClick(position, remove);
            }
            else
            {
                if (entryLineMap == null)
                    return;
                int line = (int)position.y / questLogLabel.LineHeight;

                if (line < entryLineMap.Count)
                    selectedEntry = entryLineMap[line];
                else
                    selectedEntry = entryLineMap[entryLineMap.Count - 1];

                Debug.Log($"Line is: {line} entry: {selectedEntry}");

                if (ConvenientQuestLogWindow.selectedQuestDisplayed)
                {
                    currentMessageIndex = 0;
                    SetTextActiveQuests();
                }
                else
                {
                    //ensure nothing happens when last empty line is clicked
                    if (line + 1 >= entryLineMap.Count)
                        return;
                    if (line == 0 || entryLineMap[line - 1] != selectedEntry)
                    {
                        currentMessageIndex = 0;
                        selectedQuestMessage = groupedQuestMessages[selectedEntry];
                        if (remove)
                        {
                            if ((RegisterConvenientQuestLogWindow.MainQuestMandatoryList.Contains(selectedQuestMessage.ParentQuest.QuestName) ||
                                 RegisterConvenientQuestLogWindow.MainQuestOptionalList.Contains(selectedQuestMessage.ParentQuest.QuestName)) ||
                                 selectedQuestMessage.ParentQuest.QuestName.StartsWith("S0000"))
                            {
                                DaggerfallMessageBox messageBox = new DaggerfallMessageBox(uiManager, uiManager.TopWindow);
                                messageBox.SetText(string.Format(cancelMainStory, selectedQuestMessage.ParentQuest.DisplayName, selectedQuestMessage.ParentQuest.UID));
                                messageBox.ClickAnywhereToClose = true;
                                messageBox.AllowCancel = false;
                                messageBox.ParentPanel.BackgroundColor = Color.clear;
                                messageBox.Show();
                            }
                            else if (selectedQuestMessage.ParentQuest.OneTime)
                            {
                                DaggerfallMessageBox messageBox = new DaggerfallMessageBox(uiManager, uiManager.TopWindow);
                                messageBox.SetText(string.Format(cancelOneTime, selectedQuestMessage.ParentQuest.DisplayName, selectedQuestMessage.ParentQuest.UID));
                                messageBox.ClickAnywhereToClose = true;
                                messageBox.AllowCancel = false;
                                messageBox.ParentPanel.BackgroundColor = Color.clear;
                                messageBox.Show();
                            }
                            else
                            {
                                DaggerfallMessageBox messageBox = new DaggerfallMessageBox(uiManager, DaggerfallMessageBox.CommonMessageBoxButtons.YesNo, string.Format(areYouSure, selectedQuestMessage.ParentQuest.DisplayName, selectedQuestMessage.ParentQuest.UID), this);
                                messageBox.ClickAnywhereToClose = true;
                                messageBox.AllowCancel = false;
                                messageBox.ParentPanel.BackgroundColor = Color.clear;
                                messageBox.OnButtonClick += CancelQuest_OnButtonClick;
                                messageBox.Show();
                            }
                        }
                        else
                        {
                            SetTextForSelectedQuest(selectedQuestMessage);
                        }
                    }
                    else
                    {
                        if (entryLineMap[line - 1] == selectedEntry && entryLineMap[line + 1] == selectedEntry)
                            HandleQuestClicks(groupedQuestMessages[selectedEntry]);
                    }
                }
            }
        }

        protected override void DialogButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            base.DialogButton_OnMouseClick(sender, position);
            currentMessageCheck = defaultMessageCheckValue;
        }

        private string GetTravelTime(Place place)
        {
            DFLocation dfLocation;
            DaggerfallUnity.Instance.ContentReader.GetLocation(place.SiteDetails.regionName, place.SiteDetails.locationName, out dfLocation);

            if (dfLocation.LocationIndex == GameManager.Instance.PlayerGPS.CurrentLocation.LocationIndex)
                return currentLocation;

            DFPosition position = MapsFile.LongitudeLatitudeToMapPixel(dfLocation.MapTableData.Longitude, dfLocation.MapTableData.Latitude);

            if (travelOptionsModEnabled && travelOptionsCautiousTravel && travelOptionsStopAtInnsTravel)
            {
                TransportManager transportManager = GameManager.Instance.TransportManager;
                bool useHorse = transportManager.TransportMode == TransportModes.Horse;
                bool useCart = transportManager.TransportMode == TransportModes.Cart;

                int travelTimeTotalMins = travelTimeCalculator.CalculateTravelTime(position,
                    speedCautious: !travelOptionsCautiousTravel,
                    sleepModeInn: !travelOptionsStopAtInnsTravel,
                    travelShip: false,
                    hasHorse: useHorse,
                    hasCart: useCart);
                travelTimeTotalMins = GameManager.Instance.GuildManager.FastTravel(travelTimeTotalMins);

                //TODO: can make this calc dynamic based on mod settings
                float travelTimeMinsMult = (travelOptionsCautiousTravel ? 0.8f : 1.0f) * 2;
                travelTimeTotalMins = (int)(travelTimeTotalMins / travelTimeMinsMult);

                return string.Format(travelOptionsDuration, travelTimeTotalMins / 60, travelTimeTotalMins % 60);
            }
            bool hasHorse = GameManager.Instance.TransportManager.HasHorse();
            bool hasCart = GameManager.Instance.TransportManager.HasCart();
            int minutesToTravel = GameManager.Instance.GuildManager.FastTravel(travelTimeCalculator.CalculateTravelTime(position, true, true, false, hasHorse, hasCart));
            int daysToTravel = minutesToTravel / 1440;
            if (minutesToTravel % 1440 > 0)
                ++daysToTravel;

            return string.Format(travelDuration, daysToTravel);
        }

        protected override void SetTextActiveQuests()
        {
            ConvenientQuestLogWindow.selectedQuestDisplayed = false;
            if (questMessages == null)
                return;
            messageCount = questMessages.Count;
            questLogLabel.TextScale = 1.0f;
            titleLabel.Text = TextManager.Instance.GetLocalizedText("activeQuests", TextCollections.Internal, false);
            titleLabel.ToolTip = defaultToolTip;
            titleLabel.ToolTipText = journalToolTipText;
            int num = 0;
            entryLineMap = new List<int>(20);
            List<TextFile.Token> allActiveQuests = new List<TextFile.Token>();
            groupedQuestMessages = new List<Message>();
            foreach (Quest quest in questMessages.Select(x => x.ParentQuest).Distinct())
            {
                Quest parent = quest;
                Quest.LogEntry lastLogEntry = parent.GetLogMessages().OrderBy(x => x.stepID).Last();
                groupedQuestMessages.Add(questMessages.Single(x => x.ParentQuest == parent && x.ID == lastLogEntry.messageID));
            }
            for (int i = currentMessageIndex; i < groupedQuestMessages.Count && num < 20; i++)
            {
                string title = FormatQuestTitle(groupedQuestMessages[i].ParentQuest.DisplayName, groupedQuestMessages[i].ParentQuest.QuestName);

                bool haveDurationTokens = false;

                if (RegisterConvenientQuestLogWindow.useDurationTokenSetting)
                {
                    haveDurationTokens = questMessages.Where(x => x.ParentQuest == groupedQuestMessages[i].ParentQuest).Any(y => y.Variants.Any(z => z.tokens.Any(a => a.text.Contains(durationSearchToken)))); ;
                }

                if (!RegisterConvenientQuestLogWindow.useDurationTokenSetting || (RegisterConvenientQuestLogWindow.useDurationTokenSetting && haveDurationTokens))
                {
                    List<Clock> clocks = new List<Clock>();
                    foreach (Clock clockResource in groupedQuestMessages[i].ParentQuest.GetAllResources(typeof(Clock)).Cast<Clock>())
                    {
                        if (clockResource.Enabled)
                            clocks.Add(clockResource);
                    }
                    if (clocks.Any())
                    {
                        Clock clock = clocks.OrderBy(x => x.RemainingTimeInSeconds).First();
                        if (RegisterConvenientQuestLogWindow.useDetailedQuestDurationSetting)
                        {
                            string timeLeft = clock.GetTimeString(clock.RemainingTimeInSeconds);

                            int daysLeft = Convert.ToInt32(timeLeft.Split('.')[0]);
                            int hours = Convert.ToInt32(timeLeft.Split('.')[1].Split(':')[0]);
                            int minutes = Convert.ToInt32(timeLeft.Split('.')[1].Split(':')[1]);

                            string daysString = daysLeft == 1 ? day : days;

                            title = string.Format(questTimeLimitExtendedFormat, title, daysLeft, daysString, hours, minutes);
                        }
                        else
                        {
                            string daysLeft = clock.GetDaysString(clock.RemainingTimeInSeconds);
                            string daysString = daysLeft == "1" ? day : days;
                            title = string.Format(questTimeLimitFormat, title, daysLeft, daysString);
                        }
                    }
                }
                List<TextFile.Token> oneQuestTokens = new List<TextFile.Token>() { new TextFile.Token(TextFile.Formatting.Text, title) };
                Place mentionedInMessage = GetLastPlaceMentionedInMessage(groupedQuestMessages[i]);
                string location = string.Empty;
                if (!string.IsNullOrWhiteSpace(mentionedInMessage?.SiteDetails.locationName))
                {
                    location = TextManager.Instance.GetLocalizedLocationName(mentionedInMessage.SiteDetails.mapId, mentionedInMessage.SiteDetails.locationName) + " (" + GetTravelTime(mentionedInMessage) + ")";
                    oneQuestTokens.Add(TextFile.NewLineToken);
                    oneQuestTokens.Add(new TextFile.Token(TextFile.Formatting.TextHighlight, location));
                }
                oneQuestTokens.Add(new TextFile.Token(TextFile.Formatting.Nothing, string.Empty));
                for (int index = 0; index < oneQuestTokens.Count && num < 20; ++index)
                {
                    TextFile.Token token = oneQuestTokens[index];
                    if (token.formatting == TextFile.Formatting.Text || token.formatting == TextFile.Formatting.TextHighlight)
                    {
                        ++num;
                        entryLineMap.Add(i);
                    }
                    else
                        token.formatting = TextFile.Formatting.JustifyLeft;
                    allActiveQuests.Add(token);
                }
                allActiveQuests.Add(TextFile.NewLineToken);
                ++num;
                entryLineMap.Add(i);
            }
            questLogLabel.SetText(allActiveQuests.ToArray());
        }

        private void SetTextForSelectedQuest(Message message)
        {
            ConvenientQuestLogWindow.selectedQuestDisplayed = true;
            if (questMessages == null)
                return;

            messageCount = questMessages.Count;
            questLogLabel.TextScale = 1.0f;
            List<Message> list = questMessages.Where(x => x.ParentQuest.UID == message.ParentQuest.UID).ToList();

            // We were asked to show deleted quests.
            // Cancelled, completed or by mistake
            if (list.Count == 0)
            {
                SetTextActiveQuests();
                return;
            }

            titleLabel.Text = FormatQuestTitle(list.First().ParentQuest.DisplayName, list.First().ParentQuest.QuestName);
            titleLabel.ToolTip = defaultToolTip;
            titleLabel.ToolTipText = activeQuestToolTipText;
            int num = 0;
            entryLineMap = new List<int>(20);
            List<TextFile.Token> tokenList = new List<TextFile.Token>();
            for (int currentMessageIndex = this.currentMessageIndex; currentMessageIndex < list.Count && num < 20; ++currentMessageIndex)
            {
                TextFile.Token[] textTokens = list[currentMessageIndex].GetTextTokens(-1, true);
                for (int index = 0; index < textTokens.Length && num < 20; ++index)
                {
                    TextFile.Token token = textTokens[index];
                    if (token.formatting == TextFile.Formatting.Text)
                    {
                        ++num;
                        entryLineMap.Add(0);
                    }
                    else
                        token.formatting = TextFile.Formatting.JustifyLeft;
                    tokenList.Add(token);
                }
                tokenList.Add(TextFile.NewLineToken);
                ++num;
                entryLineMap.Add(0);
            }
            questLogLabel.SetText(tokenList.ToArray());
        }

        private string FormatQuestTitle(string questTitle, string questName)
        {
            if (questTitle == "Main Quest Backbone")
                questTitle = questTitle.Replace(" Backbone", string.Empty);
            if (RegisterConvenientQuestLogWindow.IdentifyMainQuests && (RegisterConvenientQuestLogWindow.MainQuestMandatoryList.Contains(questName) || 
			    RegisterConvenientQuestLogWindow.MainQuestOptionalList.Contains(questName)))
                if (questTitle != "Main Quest")
                    questTitle = questTitle + " - Main Quest";

            return !string.IsNullOrWhiteSpace(questTitle) ? questTitle : untitledQuest;
        }

        private void CancelQuest_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
                QuestMachine.Instance.TombstoneQuest(selectedQuestMessage.ParentQuest);

            sender.CloseWindow();
            DisplayMode = DaggerfallQuestJournalWindow.JournalDisplay.ActiveQuests;
            uiManager.PushWindow(this);
        }

        private void LoadText()
        {
            const string csvFilename = "ConvenientQuestLog.csv";

            if (stringTable != null)
                return;

            stringTable = StringTableCSVParser.LoadDictionary(csvFilename);

            if (stringTable.ContainsKey("untitledQuest"))
                untitledQuest = stringTable["untitledQuest"];

            if (stringTable.ContainsKey("activeQuestToolTipText"))
                activeQuestToolTipText = stringTable["activeQuestToolTipText"];

            if (stringTable.ContainsKey("journalToolTipText"))
                journalToolTipText = stringTable["journalToolTipText"];

            if (stringTable.ContainsKey("cancelMainStory"))
                cancelMainStory = stringTable["cancelMainStory"];

            if (stringTable.ContainsKey("cancelOneTime"))
                cancelOneTime = stringTable["cancelOneTime"];

            if (stringTable.ContainsKey("areYouSure"))
                areYouSure = stringTable["areYouSure"];

            if (stringTable.ContainsKey("currentLocation"))
                currentLocation = stringTable["currentLocation"];

            if (stringTable.ContainsKey("travelOptionsDuration"))
                travelOptionsDuration = stringTable["travelOptionsDuration"];

            if (stringTable.ContainsKey("travelDuration"))
                travelDuration = stringTable["travelDuration"];

            if (stringTable.ContainsKey("durationSearchToken"))
                durationSearchToken = stringTable["durationSearchToken"];

            if (stringTable.ContainsKey("day"))
                day = stringTable["day"];

            if (stringTable.ContainsKey("days"))
                days = stringTable["days"];

            if (stringTable.ContainsKey("questTimeLimitFormat"))
                questTimeLimitFormat = stringTable["questTimeLimitFormat"];

            if (stringTable.ContainsKey("questTimeLimitExtendedFormat"))
                questTimeLimitExtendedFormat = stringTable["questTimeLimitExtendedFormat"];
        }
    }
}
