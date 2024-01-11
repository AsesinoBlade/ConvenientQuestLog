using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;


public class RegisterConvenientQuestLogWindow : MonoBehaviour
{
    public static Mod mod;
    public static RegisterConvenientQuestLogWindow instance;
    public static bool IdentifyMainQuests = false;
    public static int MessageDelay = 10;
    public static bool useDurationTokenSetting = false;
    public static bool useDetailedQuestDurationSetting = false;

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


        // Register Custom UI Window
        UIWindowFactory.RegisterCustomUIWindow(UIWindowType.QuestJournal, typeof(ConvenientQuestLogWindow));


        // Add script to the scene.
        instance = new GameObject("ConvenientQuestLog").AddComponent<RegisterConvenientQuestLogWindow>();
    }

    public void Start()
    {
        QuestMachine.OnQuestStarted += QuestMachineOnOnQuestStarted;
    }

    private void QuestMachineOnOnQuestStarted(Quest quest)
    {
        if (!IdentifyMainQuests)
            return;

        if (MessageDelay > 0)
        {
            if (MainQuestMandatoryList.Contains(quest.QuestName))
            {
                DaggerfallUI.AddHUDText("*** MAIN QUEST MANDATORY ***", MessageDelay);
            }
            else if (MainQuestOptionalList.Contains(quest.QuestName))
            {
                DaggerfallUI.AddHUDText("*** MAIN QUEST OPTIONAL ***", MessageDelay);

            }
        }
        else
        {
            if (MainQuestMandatoryList.Contains(quest.QuestName))
            {
                DaggerfallUI.AddHUDText("*** MAIN QUEST MANDATORY ***");
            }
            else if (MainQuestOptionalList.Contains(quest.QuestName))
            {
                DaggerfallUI.AddHUDText("*** MAIN QUEST OPTIONAL ***");

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
        MessageDelay = settings.GetValue<int>("Option", "MessageDelay");
        useDurationTokenSetting = settings.GetValue<bool>("General", "QuestsShouldContainDurationToken");
        useDetailedQuestDurationSetting = settings.GetValue<bool>("General", "DetailedQuestDuration");
    }
}
