using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

public class MovieShootMod : Script
{
    private int currentStage = 0; // 0 - ожидание, 1 - подготовка, 2 - бой, 3 - награждение, 4 - откат
    private int currentSceneIndex = 0;
    private bool isModActive = true;

    private readonly string[] sceneTitles = new string[]
    {
        "Стадия 1: Уличная разборка",
        "Стадия 2: Приход парней и месть (Драка)",
        "Стадия 3: Приезд машин с 10 Балласами",
        "Стадия 4: Перестрелка с Балласами",
        "Стадия 5: Финал и Награждение"
    };

    private Vector3 stageCenter = new Vector3(-1177.312f, -507.5424f, 35.5662f);

    private bool isInitialized = false;
    private Blip stageBlip;
    private List<Ped> crewPeds = new List<Ped>();
    private List<Ped> currentWaveEnemies = new List<Ped>();
    private List<Ped> allSpawnedEnemies = new List<Ped>();
    private List<Vehicle> sceneVehicles = new List<Vehicle>();
    private List<Prop> sceneProps = new List<Prop>();

    private Ped directorPed;
    private Ped cameramanPed;
    private Ped soundGuyPed;

    private int gangsterRelGroup;
    private int crewRelGroup;
    private int stageTimer = 0;

    public MovieShootMod()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void InitializeMod()
    {
        gangsterRelGroup = World.AddRelationshipGroup("MOVIE_GANGSTERS");
        crewRelGroup = World.AddRelationshipGroup("MOVIE_CREW");

        CreateStudioBlip();

        UI.Notify("~g~MovieShootMod (Чистый геймплейный режим) загружен!");
        isInitialized = true;
    }

    private void CreateStudioBlip()
    {
        if (stageBlip == null || !stageBlip.Exists())
        {
            stageBlip = World.CreateBlip(stageCenter);
            Function.Call(Hash.SET_BLIP_SPRITE, stageBlip.Handle, 135);
            Function.Call(Hash.SET_BLIP_COLOUR, stageBlip.Handle, 5);
        }
    }

    private void RemoveStudioBlip()
    {
        if (stageBlip != null && stageBlip.Exists())
        {
            stageBlip.Remove();
            stageBlip = null;
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (!isInitialized)
        {
            InitializeMod();
        }

        if (!isModActive) return;

        Ped player = Game.Player.Character;

        if (player.IsDead && currentStage != 0)
        {
            StopHandheldCameraEffect();
            FullCleanup();
            UI.ShowSubtitle("~r~Съемка сорвана! Актер погиб.", 3500);
            currentSceneIndex = 0;
            currentStage = 0;
            return;
        }

        if (currentStage == 1 || currentStage == 2)
        {
            UpdateCrewTracking(player);
        }

        if (currentStage == 0)
        {
            float dist = World.GetDistance(player.Position, stageCenter);

            if (dist < 100.0f)
            {
                Function.Call(Hash.DRAW_MARKER, 1,
                    stageCenter.X, stageCenter.Y, stageCenter.Z - 1.0f,
                    0.0f, 0.0f, 0.0f,
                    0.0f, 0.0f, 0.0f,
                    2.2f, 2.2f, 1.2f,
                    255, 215, 0, 180,
                    false, false, 2, false, 0, 0, false);
            }

            if (dist < 3.0f)
            {
                UI.ShowSubtitle("~y~Снять сцены фильма?~w~ Нажмите ~g~[Y]~w~ - Да, ~r~[N]~w~ - Нет (~y~[F10]~w~ - Вкл/Выкл мод)", 100);
            }
        }
        else if (currentStage == 1)
        {
            if (Game.Player.WantedLevel > 0)
                Game.Player.WantedLevel = 0;

            if (Game.GameTime > stageTimer)
            {
                if (currentSceneIndex == 2)
                {
                    currentSceneIndex = 3;
                    StartActionPhase();
                }
                else
                {
                    StartActionPhase();
                }
            }
        }
        else if (currentStage == 2)
        {
            if (Game.Player.WantedLevel > 0)
                Game.Player.WantedLevel = 0;

            bool waveDead = true;
            for (int i = 0; i < currentWaveEnemies.Count; i++)
            {
                Ped g = currentWaveEnemies[i];
                if (g != null && g.Exists() && !g.IsDead)
                {
                    waveDead = false;
                    break;
                }
            }

            if (waveDead)
            {
                AdvanceToNextStage();
            }
        }
        else if (currentStage == 3)
        {
            // СТАДИЯ 5: Награждение
            if (Game.Player.WantedLevel > 0)
                Game.Player.WantedLevel = 0;

            if (Game.GameTime > stageTimer)
            {
                FullCleanup();
                UI.ShowSubtitle("~g~Площадка свободна для следующего дубля!", 4000);
                stageTimer = Game.GameTime + 3000;
                currentStage = 4;
            }
        }
        else if (currentStage == 4)
        {
            if (Game.GameTime > stageTimer)
            {
                currentStage = 0;
            }
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F10)
        {
            isModActive = !isModActive;
            if (!isModActive)
            {
                StopHandheldCameraEffect();
                FullCleanup();
                RemoveStudioBlip();
                currentStage = 0;
                currentSceneIndex = 0;
                UI.Notify("~r~Киномод ВЫКЛ");
            }
            else
            {
                CreateStudioBlip();
                UI.Notify("~g~Киномод ВКЛ");
            }
            return;
        }

        if (!isModActive) return;

        if (currentStage == 0)
        {
            float dist = World.GetDistance(Game.Player.Character.Position, stageCenter);
            if (dist < 3.0f)
            {
                if (e.KeyCode == Keys.Y)
                {
                    StartFullMovieProduction();
                }
                else if (e.KeyCode == Keys.N)
                {
                    UI.ShowSubtitle("~r~Вы отказались от съемок.", 2500);
                    stageTimer = Game.GameTime + 5000;
                    currentStage = 4;
                }
            }
        }
    }

    private void PreloadAllAssets()
    {
        string[] modelsToLoad = new string[]
        {
            "s_m_m_movprem_01", "s_m_m_cntrybar_01", "s_m_m_autoshop_01",
            "p_amb_clipboard_01", "prop_v_cam_01", "prop_v_bmike_01",
            "g_m_y_famdnf_01", "g_m_y_ballasout_01", "g_m_y_ballaorig_01", "cavalcade2"
        };

        for (int i = 0; i < modelsToLoad.Length; i++)
        {
            Model m = new Model(modelsToLoad[i]);
            m.Request(1000);
        }

        string[] animDicts = new string[]
        {
            "amb@world_human_clipboard@male@base",
            "missfinale_c2mcs_1",
            "missfra1"
        };

        for (int i = 0; i < animDicts.Length; i++)
        {
            Function.Call(Hash.REQUEST_ANIM_DICT, animDicts[i]);
        }
    }

    private void StartFullMovieProduction()
    {
        FullCleanup();
        PreloadAllAssets();
        currentSceneIndex = 0;

        Ped player = Game.Player.Character;

        Vector3 directorPos = player.GetOffsetInWorldCoords(new Vector3(-1.8f, -3.8f, 0.0f));
        Vector3 camPos = player.GetOffsetInWorldCoords(new Vector3(0.0f, -3.8f, 0.0f));
        Vector3 soundPos = player.GetOffsetInWorldCoords(new Vector3(1.8f, -3.8f, 0.0f));

        directorPed = SpawnCrewPed("s_m_m_movprem_01", directorPos, player.Heading);
        cameramanPed = SpawnCrewPed("s_m_m_cntrybar_01", camPos, player.Heading);
        soundGuyPed = SpawnCrewPed("s_m_m_autoshop_01", soundPos, player.Heading);

        if (directorPed != null)
        {
            AttachPropConfigured(directorPed, "p_amb_clipboard_01", 60309,
                new Vector3(-0.01f, 0.0f, 0.0f),
                new Vector3(0.0f, 0.0f, 0.0f));
            PlayLoopingAnim(directorPed, "amb@world_human_clipboard@male@base", "base");
        }

        if (cameramanPed != null)
        {
            AttachPropConfigured(cameramanPed, "prop_v_cam_01", 28422,
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 0.0f, 0.0f));
            PlayLoopingAnim(cameramanPed, "missfinale_c2mcs_1", "fin_c2_mcs_1_camman");
        }

        if (soundGuyPed != null)
        {
            AttachPropConfigured(soundGuyPed, "prop_v_bmike_01", 28422,
                new Vector3(-0.05f, 0.02f, -0.03f),
                new Vector3(0.0f, 0.0f, -85.0f));
            PlayLoopingAnim(soundGuyPed, "missfra1", "mcs2_crew_idle_m_boom");
        }

        StartHandheldCameraEffect();
        SetupWaveForCurrentStage();
    }

    private void SetupWaveForCurrentStage()
    {
        currentWaveEnemies.Clear();

        Ped player = Game.Player.Character;
        int playerGroup = player.RelationshipGroup;

        World.SetRelationshipBetweenGroups(Relationship.Respect, gangsterRelGroup, playerGroup);
        World.SetRelationshipBetweenGroups(Relationship.Respect, playerGroup, gangsterRelGroup);
        World.SetRelationshipBetweenGroups(Relationship.Respect, gangsterRelGroup, crewRelGroup);
        World.SetRelationshipBetweenGroups(Relationship.Respect, crewRelGroup, gangsterRelGroup);

        if (currentSceneIndex == 0)
        {
            SpawnFootGangsters(player, "g_m_y_famdnf_01", 3, false);
            UI.ShowSubtitle("~y~" + sceneTitles[0] + "~w~\nРежиссер: Камера... Мотор!", 2500);
            stageTimer = Game.GameTime + 2500;
        }
        else if (currentSceneIndex == 1)
        {
            SpawnFootGangsters(player, "g_m_y_ballasout_01", 4, false);
            UI.ShowSubtitle("~y~" + sceneTitles[1] + "~w~\nРежиссер: Продолжаем дубль! Парни бегут мстить!", 2000);
            stageTimer = Game.GameTime + 2000;
        }
        else if (currentSceneIndex == 2)
        {
            SpawnBallasCarArrival(player);
            UI.ShowSubtitle("~y~" + sceneTitles[2] + "~w~\nРежиссер: Не выключаем камеру! Машины с 10 Балласами!", 4500);
            stageTimer = Game.GameTime + 4500;
        }

        Audio.PlaySoundFrontend("5_SEC_WARNING", "HUD_MINI_GAME_SOUNDSET");
        currentStage = 1;
    }

    private void SpawnFootGangsters(Ped player, string modelName, int count, bool giveGuns)
    {
        Model gModel = new Model(modelName);
        if (!gModel.IsLoaded) gModel.Request(500);

        for (int i = 0; i < count; i++)
        {
            float spreadX = (i - (count - 1) / 2.0f) * 2.0f;
            Vector3 spawnPos = player.GetOffsetInWorldCoords(new Vector3(spreadX, 7.0f, 0.0f));

            if (gModel.IsLoaded)
            {
                Ped g = World.CreatePed(gModel, spawnPos);
                if (g != null && g.Exists())
                {
                    g.RelationshipGroup = gangsterRelGroup;
                    if (giveGuns)
                        g.Weapons.Give(WeaponHash.MicroSMG, 300, true, true);
                    else
                        g.Weapons.RemoveAll();

                    g.BlockPermanentEvents = true;
                    Function.Call(Hash.TASK_TURN_PED_TO_FACE_ENTITY, g.Handle, player.Handle, -1);
                    currentWaveEnemies.Add(g);
                    allSpawnedEnemies.Add(g);
                }
            }
        }
        gModel.MarkAsNoLongerNeeded();
    }

    private void SpawnBallasCarArrival(Ped player)
    {
        int[] pedsPerCar = new int[] { 4, 4, 2 };

        Model carModel = new Model("cavalcade2");
        if (!carModel.IsLoaded) carModel.Request(500);

        Model gModel = new Model("g_m_y_ballaorig_01");
        if (!gModel.IsLoaded) gModel.Request(500);

        for (int c = 0; c < 3; c++)
        {
            float offsetX = (c - 1) * 4.2f;
            Vector3 carSpawnPos = player.GetOffsetInWorldCoords(new Vector3(offsetX, 16.0f, 0.5f));
            Vector3 carTargetPos = player.GetOffsetInWorldCoords(new Vector3(offsetX, 8.0f, 0.0f));

            if (carModel.IsLoaded)
            {
                Vehicle car = World.CreateVehicle(carModel, carSpawnPos, player.Heading + 180.0f);

                if (car != null && car.Exists())
                {
                    Function.Call(Hash.SET_VEHICLE_COLOURS, car.Handle, 145, 145);
                    car.EngineRunning = true;
                    sceneVehicles.Add(car);

                    int passengersToSpawn = pedsPerCar[c];
                    for (int s = 0; s < passengersToSpawn; s++)
                    {
                        if (gModel.IsLoaded)
                        {
                            int seatIndex = (s == 0) ? -1 : (s - 1);
                            Ped g = Function.Call<Ped>(Hash.CREATE_PED_INSIDE_VEHICLE, car.Handle, 26, gModel.Hash, seatIndex, true, true);

                            if (g != null && g.Exists())
                            {
                                g.RelationshipGroup = gangsterRelGroup;
                                g.Weapons.Give(WeaponHash.MicroSMG, 300, true, true);
                                g.BlockPermanentEvents = true;
                                currentWaveEnemies.Add(g);
                                allSpawnedEnemies.Add(g);

                                if (seatIndex == -1)
                                {
                                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, g.Handle, car.Handle,
                                        carTargetPos.X, carTargetPos.Y, carTargetPos.Z,
                                        14.0f, 1, car.Model.Hash, 16777216, 2.5f, true);
                                }
                            }
                        }
                    }
                }
            }
        }

        carModel.MarkAsNoLongerNeeded();
        gModel.MarkAsNoLongerNeeded();
    }

    private void StartActionPhase()
    {
        Ped player = Game.Player.Character;
        int playerGroup = player.RelationshipGroup;

        Audio.PlaySoundFrontend("GO", "HUD_MINI_GAME_SOUNDSET");
        UI.ShowSubtitle("~r~Режиссер: СТАРТ! " + sceneTitles[currentSceneIndex] + "!", 3000);

        World.SetRelationshipBetweenGroups(Relationship.Hate, gangsterRelGroup, playerGroup);
        World.SetRelationshipBetweenGroups(Relationship.Hate, playerGroup, gangsterRelGroup);

        for (int i = 0; i < currentWaveEnemies.Count; i++)
        {
            Ped g = currentWaveEnemies[i];
            if (g != null && g.Exists() && !g.IsDead)
            {
                g.BlockPermanentEvents = false;
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, g.Handle, 46, true);

                if (g.IsInVehicle())
                {
                    Function.Call(Hash.TASK_LEAVE_ANY_VEHICLE, g.Handle, 0, 0);
                }

                g.Task.FightAgainst(player);
            }
        }

        currentStage = 2;
    }

    private void AdvanceToNextStage()
    {
        if (currentSceneIndex == 0)
        {
            currentSceneIndex = 1;
            SetupWaveForCurrentStage();
        }
        else if (currentSceneIndex == 1)
        {
            currentSceneIndex = 2;
            SetupWaveForCurrentStage();
        }
        else if (currentSceneIndex >= 3)
        {
            EnterStage5AwardCeremony();
        }
    }

    private void EnterStage5AwardCeremony()
    {
        StopHandheldCameraEffect();

        Audio.PlaySoundFrontend("MISSION_PASS_NOTIFY", "DLC_HEISTS_GENERAL_FRONTEND_SOUNDS");
        Game.Player.Money += 10000;

        UI.Notify("~y~ФИНАЛ: НАГРАЖДЕНИЕ!~w~ Съемка завершена.");
        UI.ShowSubtitle("~g~Режиссер: СНЯТО! Отличная работа! Держи гонорар $10,000!", 6000);

        for (int i = 0; i < sceneProps.Count; i++)
        {
            if (sceneProps[i] != null && sceneProps[i].Exists())
            {
                Function.Call(Hash.DETACH_ENTITY, sceneProps[i].Handle, true, true);
                sceneProps[i].IsVisible = false;
                sceneProps[i].MarkAsNoLongerNeeded();
            }
        }
        sceneProps.Clear();

        if (directorPed != null && directorPed.Exists())
        {
            Function.Call(Hash.CLEAR_PED_TASKS, directorPed.Handle);
            Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, directorPed.Handle, "WORLD_HUMAN_CHEERING", 0, true);
        }
        if (cameramanPed != null && cameramanPed.Exists())
        {
            Function.Call(Hash.CLEAR_PED_TASKS, cameramanPed.Handle);
            Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, cameramanPed.Handle, "WORLD_HUM_CHEERING", 0, true);
        }
        if (soundGuyPed != null && soundGuyPed.Exists())
        {
            Function.Call(Hash.CLEAR_PED_TASKS, soundGuyPed.Handle);
            Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, soundGuyPed.Handle, "WORLD_HUM_CHEERING", 0, true);
        }

        stageTimer = Game.GameTime + 6000;
        currentStage = 3;
    }

    private void StartHandheldCameraEffect()
    {
        Function.Call(Hash.SHAKE_GAMEPLAY_CAM, "HAND_SHAKE", 0.65f);
    }

    private void StopHandheldCameraEffect()
    {
        Function.Call(Hash.STOP_GAMEPLAY_CAM_SHAKING, true);
    }

    private void UpdateCrewTracking(Ped player)
    {
        if (directorPed != null && directorPed.Exists())
        {
            FacePedTowardsTarget(directorPed, player.Position);
            if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, directorPed.Handle, "amb@world_human_clipboard@male@base", "base", 3))
            {
                Function.Call(Hash.TASK_PLAY_ANIM, directorPed.Handle, "amb@world_human_clipboard@male@base", "base",
                    8.0f, -8.0f, -1, 49, 0.0f, false, false, false);
            }
        }

        if (cameramanPed != null && cameramanPed.Exists())
        {
            FacePedTowardsTarget(cameramanPed, player.Position);
            if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, cameramanPed.Handle, "missfinale_c2mcs_1", "fin_c2_mcs_1_camman", 3))
            {
                Function.Call(Hash.TASK_PLAY_ANIM, cameramanPed.Handle, "missfinale_c2mcs_1", "fin_c2_mcs_1_camman",
                    8.0f, -8.0f, -1, 49, 0.0f, false, false, false);
            }
        }

        if (soundGuyPed != null && soundGuyPed.Exists())
        {
            FacePedTowardsTarget(soundGuyPed, player.Position);
            if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, soundGuyPed.Handle, "missfra1", "mcs2_crew_idle_m_boom", 3))
            {
                Function.Call(Hash.TASK_PLAY_ANIM, soundGuyPed.Handle, "missfra1", "mcs2_crew_idle_m_boom",
                    8.0f, -8.0f, -1, 49, 0.0f, false, false, false);
            }
        }
    }

    private void FacePedTowardsTarget(Ped ped, Vector3 targetPos)
    {
        Vector3 dir = targetPos - ped.Position;
        float angle = (float)(Math.Atan2(dir.Y, dir.X) * (180.0 / Math.PI)) - 90.0f;
        if (angle < 0.0f) angle += 360.0f;
        ped.Heading = angle;
    }

    private Ped SpawnCrewPed(string modelName, Vector3 pos, float heading)
    {
        Model model = new Model(modelName);
        if (!model.IsLoaded) model.Request(1000);
        if (!model.IsLoaded) return null;

        Ped ped = World.CreatePed(model, pos, heading);
        model.MarkAsNoLongerNeeded();

        if (ped != null && ped.Exists())
        {
            ped.RelationshipGroup = crewRelGroup;
            ped.IsInvincible = true;
            ped.BlockPermanentEvents = true;
            Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped.Handle, 0, false);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 17, false);
            crewPeds.Add(ped);
        }
        return ped;
    }

    private void PlayLoopingAnim(Ped ped, string animDict, string animName)
    {
        if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animDict))
        {
            Function.Call(Hash.REQUEST_ANIM_DICT, animDict);
        }

        Function.Call(Hash.TASK_PLAY_ANIM, ped.Handle, animDict, animName,
            8.0f, -8.0f, -1, 49, 0.0f, false, false, false);
    }

    private void AttachPropConfigured(Ped ped, string propName, int boneId, Vector3 offset, Vector3 rotation)
    {
        Model propModel = new Model(propName);
        if (!propModel.IsLoaded) propModel.Request(1000);
        if (!propModel.IsLoaded) return;

        Prop prop = World.CreateProp(propModel, ped.Position, false, false);
        propModel.MarkAsNoLongerNeeded();

        if (prop != null && prop.Exists())
        {
            Function.Call(Hash.SET_ENTITY_COLLISION, prop.Handle, false, false);

            int boneIndex = Function.Call<int>(Hash.GET_PED_BONE_INDEX, ped.Handle, boneId);
            Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY, prop.Handle, ped.Handle, boneIndex,
                offset.X, offset.Y, offset.Z,
                rotation.X, rotation.Y, rotation.Z,
                true, true, false, true, 1, true);
            sceneProps.Add(prop);
        }
    }

    private void FullCleanup()
    {
        currentWaveEnemies.Clear();

        for (int i = 0; i < allSpawnedEnemies.Count; i++)
        {
            if (allSpawnedEnemies[i] != null && allSpawnedEnemies[i].Exists())
            {
                allSpawnedEnemies[i].Delete();
            }
        }
        allSpawnedEnemies.Clear();

        for (int i = 0; i < sceneVehicles.Count; i++)
        {
            if (sceneVehicles[i] != null && sceneVehicles[i].Exists())
            {
                sceneVehicles[i].Delete();
            }
        }
        sceneVehicles.Clear();

        for (int i = 0; i < crewPeds.Count; i++)
        {
            if (crewPeds[i] != null && crewPeds[i].Exists())
            {
                crewPeds[i].Delete();
            }
        }
        crewPeds.Clear();

        for (int i = 0; i < sceneProps.Count; i++)
        {
            if (sceneProps[i] != null && sceneProps[i].Exists())
            {
                sceneProps[i].Delete();
            }
        }
        sceneProps.Clear();

        directorPed = null;
        cameramanPed = null;
        soundGuyPed = null;
    }

    private void OnAborted(object sender, EventArgs e)
    {
        StopHandheldCameraEffect();
        FullCleanup();
        RemoveStudioBlip();
    }
}