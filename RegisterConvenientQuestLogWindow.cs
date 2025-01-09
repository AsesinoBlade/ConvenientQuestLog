using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Linq;
using DaggerfallWorkshop.Utility;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Localization;


public class RegisterConvenientQuestLogWindow : MonoBehaviour
{
    public static Mod mod;
    public static RegisterConvenientQuestLogWindow instance;
    public static bool IdentifyMainQuests = false;
    public static bool IdentifyMainQuestOptionalVsMandatory = false;
    public static int MessageDelay = 10;
    public static bool useDurationTokenSetting = false;
    public static bool useDetailedQuestDurationSetting = false;
    public static string MandatoryMessage = string.Empty;
    public static string OptionalMessage = string.Empty;

    public static List<string> MainQuestMandatoryList = new List<string>()
    {
        "S0000001",
        "S0000003",
        "S0000004",
        "S0000007",
        "S0000008",
        "S0000010",
        "S0000012",
        "S0000015",
        "S0000016",
        "S0000017",
        "S0000018",
        "S0000020",
        "S0000021",
        "S0000022",
        "_BRISIEN",
        "Main Quest",
        "S0000999"
    };

    public static List<string> MainQuestOptionalList = new List<string>()
    {
        "S0000002",
        "S0000005",
        "S0000006",
        "S0000009",
        "S0000011",
        "S0000013",
        "S0000988"
    };

    public static RegisterConvenientQuestLogWindow Instance
    {
        get { return instance != null ? instance : (instance = FindObjectOfType<RegisterConvenientQuestLogWindow>()); }
    }

    // Use this for initialization
    [Invoke(StateManager.StateTypes.Start, 0)]
    public static void Init(InitParams initParams)
    {
        // Get mod
        RegisterConvenientQuestLogWindow.mod = initParams.Mod;

        // Add script to the scene.
        instance = new GameObject(mod.Title).AddComponent<RegisterConvenientQuestLogWindow>();
    }

    public void Start()
    {
        // Register Custom UI Window
        UIWindowFactory.RegisterCustomUIWindow(UIWindowType.QuestJournal, typeof(ConvenientQuestLogWindow));

        QuestMachine.OnQuestStarted += QuestMachineOnOnQuestStarted;
        FormulaHelper.RegisterOverride(mod, "ReplyFinishTheQuestFirst", (Func<QuestResourceBehaviour, bool>)ReplyFinishTheQuestFirst);
    }

    private bool ReplyFinishTheQuestFirst(QuestResourceBehaviour questResourceBehaviour)
    {
        QuestMacroHelper macroHelper = new QuestMacroHelper();
        string str = "";
        string[] starters = new string[]
        {
            $"Did you already forget what you need to do for {questResourceBehaviour.TargetQuest.DisplayName}, as I already said[/center]",
            "time placeholder",
            $"I don't think you finished {questResourceBehaviour.TargetQuest.DisplayName} yet.[/center]",
            $"I've already told you all I know about {questResourceBehaviour.TargetQuest.DisplayName}[/center]",
            $"I'm not sure why you are asking me about {questResourceBehaviour.TargetQuest.DisplayName},\nas I've already told you everything I know.[/center]",
        };

        Clock clock = null;
        foreach (KeyValuePair<string, QuestResource> resource in questResourceBehaviour.TargetQuest.resources.Where(x => x.Value is Clock))
        {
            clock = (Clock)resource.Value;
            var remainingDays = clock.RemainingTimeInSeconds / 86400;
            float remainingHours = (clock.RemainingTimeInSeconds - remainingDays * 86400) / 3600f;
            int remainingHoursInt = (clock.RemainingTimeInSeconds - remainingDays * 86400) / 3600;
            var adj = "";
            if (remainingHoursInt * 3600 == clock.RemainingTimeInSeconds - remainingDays* 86400)
            {
                adj = "exactly";
            }
            else if (remainingHours - remainingHoursInt < 0.5)
            {
                adj = "less than";
            }
            else
            {
                adj = "a bit more than";
            }

            if (remainingDays > 0 && remainingHours > 0f)
                starters[1] = $"Hurry up, you only have  {(int)remainingDays} days and {adj} {remainingHoursInt} hours left\nto complete {questResourceBehaviour.TargetQuest.DisplayName}[/center]";
            else if (remainingHours > 0)
                starters[1] = $"Hurry up, you only have {adj} {remainingHoursInt} hours left\nto complete {questResourceBehaviour.TargetQuest.DisplayName}[/center]";
        }


        if (questResourceBehaviour.TargetQuest == null)
            return false;
        TextFile.Token[] tokens;

        if (questResourceBehaviour.TargetQuest.messages.ContainsKey(1002) == false)
        {
            str =
                $"Aren't you supposed to be working on {questResourceBehaviour.TargetQuest.DisplayName},\nlet me know when you are done.[/center]";
            tokens = DaggerfallStringTableImporter.ConvertStringToRSCTokens(str);
            DaggerfallUI.MessageBox(tokens);
            return false;
        }
        var selection = UnityEngine.Random.Range(0, starters.Length);

        if (selection > 0)
        {
            tokens = DaggerfallStringTableImporter.ConvertStringToRSCTokens(starters[selection]);
            DaggerfallUI.MessageBox(tokens);
            return false;
        }

        tokens = questResourceBehaviour.TargetQuest.messages[1002].GetTextTokens();
        if (tokens == null || tokens.Length == 0)
        {
            DaggerfallUI.MessageBox(
                $"Aren't you supposed to be working on {questResourceBehaviour.TargetQuest.DisplayName}, let me know when you are done.");
            return false;
        }

        macroHelper.ExpandQuestMessage(questResourceBehaviour.TargetQuest, ref tokens, true);
        str = DaggerfallStringTableImporter.ConvertRSCTokensToString(9999, tokens);
        str = starters[0] + "\n" + str;
        tokens = DaggerfallStringTableImporter.ConvertStringToRSCTokens(str);
        DaggerfallUI.MessageBox(tokens);
        return true;
    }

    private void QuestMachineOnOnQuestStarted(Quest quest)
    {
        if (!IdentifyMainQuests)
            return;
        var useForOptionalMessage = IdentifyMainQuestOptionalVsMandatory ? OptionalMessage : MandatoryMessage;
        
        if (MessageDelay > 0)
        {
            if (MainQuestMandatoryList.Contains(quest.QuestName))
            {
                DaggerfallUI.AddHUDText(MandatoryMessage, MessageDelay);
            }
            else if (MainQuestOptionalList.Contains(quest.QuestName))
            {
                DaggerfallUI.AddHUDText(useForOptionalMessage, MessageDelay);

            }
        }
        else
        {
            if (MainQuestMandatoryList.Contains(quest.QuestName))
            {
                DaggerfallUI.AddHUDText(MandatoryMessage);
            }
            else if (MainQuestOptionalList.Contains(quest.QuestName))
            {
                DaggerfallUI.AddHUDText(useForOptionalMessage);
            }
        }
    }

    public void Awake()
    {
        mod.LoadSettingsCallback = LoadSettings;
        mod.LoadSettings();
        mod.IsReady = true;
    }

    private void LoadSettings(ModSettings settings, ModSettingsChange change)
    {

        IdentifyMainQuests = settings.GetValue<bool>("Option", "IdentifyMainQuests");
        IdentifyMainQuestOptionalVsMandatory = settings.GetValue<bool>("Option", "IdentifyMainQuestOptionalVsMandatory");
        MessageDelay = settings.GetValue<int>("Option", "MessageDelay");
        useDurationTokenSetting = settings.GetValue<bool>("General", "QuestsShouldContainDurationToken");
        useDetailedQuestDurationSetting = settings.GetValue<bool>("General", "DetailedQuestDuration");
        MandatoryMessage = settings.GetValue<string>("Option", "MandatoryMessage");
        OptionalMessage = settings.GetValue<string>("Option", "OptionalMessage");
    }
}
