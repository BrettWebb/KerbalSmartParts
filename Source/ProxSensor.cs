using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSPAPIExtensions;

namespace Lib
{


    internal static class ProxChannel
    {
        public static List<ProxSensor> Listeners = new List<ProxSensor>();
    }

    class ProxSensor : PartModule
    {

        #region Fields

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Channel"), UI_FloatRange(minValue = 1f, maxValue = 20f, stepIncrement = 1f)]
        public float channel = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Group"),
            UI_ChooseOption(
            options = new String[] {
                "0",
                "1",
                "2",
                "3",
                "4",
                "5",
                "6",
                "7",
                "8",
                "9",
                "10",
                "11",
                "12",
                "13",
                "14",
                "15"
            },
            display = new String[] {
                "Stage",
                "AG1",
                "AG2",
                "AG3",
                "AG4",
                "AG5",
                "AG6",
                "AG7",
                "AG8",
                "AG9",
                "AG10",
                "Lights",
                "RCS",
                "SAS",
                "Brakes",
                "Abort"
            }
        )]
        public string group = "0";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "Distance", guiFormat = "F0", guiUnits = "m"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = 0f, maxValue = 2000f, incrementLarge = 250f, incrementSmall = 25f, incrementSlide = 1f)]
        public float meterDistance = 0;

        //AGXGroup shows if AGX installed and hides Group above
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Group"),
            UI_ChooseOption(
            options = new String[] {
                "0",
                "1",
                "11",
                "12",
                "13",
                "14",
                "15"
            },
            display = new String[] {
                "Stage",
                "Action Group:",
                "Lights",
                "RCS",
                "SAS",
                "Brakes",
                "Abort"
            }
        )]
        public string agxGroupType = "0";

        // AGX Action groups, use own slider if selected, only show this field if AGXGroup above is 1
        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = false, guiName = "Group:", guiFormat = "N0"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = 1f, maxValue = 250f, incrementLarge = 75f, incrementSmall = 25f, incrementSlide = 1f)]
        public float agxGroupNum = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Trigger on"),
            UI_ChooseOption(options = new string[] { "Both", "Approach", "Departure" })]
        public string direction = "Both";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Auto Reset"),
            UI_Toggle(disabledText = "False", enabledText = "True")]
        public bool autoReset = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Active"),
            UI_Toggle(disabledText = "False", enabledText = "True")]
        public bool isArmed = false;

        /* DEBUG CODE
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Get Count")]
        public void getCount() {
            MonoBehaviour.print(ProxChannel.Listeners.Count());
            foreach (var listener in ProxChannel.Listeners) {
                MonoBehaviour.print(" Name - " + listener.vessel.name + " ID - " + listener.vessel.id + " Channel - " listener.channel);
            }
        }*/

        #endregion

        #region Variables

        private string groupLastUpdate = "0"; //AGX: What was our selected group last update frame? Top slider.
        private double currentDistance = 0;
        private double currentWindow = 0;
        private Boolean departing = false;
        private Boolean fireNextupdate = false;
        public Boolean justRegistered = true;

        #endregion

        #region Overrides

        public override void OnStart(StartState state) {
            this.part.force_activate();
            //Clear listeners if the scene changes. Will be recreated on new scene load
            GameEvents.onGameSceneLoadRequested.Add(clearListeners);
            GameEvents.onVesselLoaded.Add(registerListener);
            //Remove from listeners if vessel or part is destroyed
            this.vessel.OnJustAboutToBeDestroyed += OnJustAboutToBeDestroyed;
            this.part.OnJustAboutToBeDestroyed += OnJustAboutToBeDestroyed;
            if (state == StartState.Editor) {
                this.part.OnEditorAttach += OnEditorAttach;
                this.part.OnEditorDetach += OnEditorDetach;
                this.part.OnEditorDestroy += OnEditorDestroy;
            }
            registerListener();
            updateButtons();
        }

        public override void OnFixedUpdate() {
            //Update target distance and determine target window
            updateDistance();
            //If the device is armed, check for the trigger altitude
            if (isArmed) {
                //We're departing. Trigger at or beyond target distance
                if (direction != "Approach" && departing && Math.Abs((currentDistance + currentWindow) - meterDistance) < currentWindow) {
                    MonoBehaviour.print("Proximity alert. Action fired on " + this.vessel.name);
                    //This flag is checked for in OnUpdate to trigger staging
                    fireNextupdate = true;
                    isArmed = false;
                }
                //We're approaching. Trigger at or closer than target distance
                else if (direction != "Departure" && !departing && Math.Abs((currentDistance - currentWindow) - meterDistance) < currentWindow) {
                    MonoBehaviour.print("Proximity alert. Action fired on " + this.vessel.name);
                    //This flag is checked for in OnUpdate to trigger staging
                    fireNextupdate = true;
                    isArmed = false;
                }
            }

            //If auto reset is enabled, wait for departure from the target window and rearm
            if (!isArmed & autoReset) {
                if (!isArmed & autoReset) {
                    if (departing && Math.Abs((currentDistance + currentWindow) - meterDistance) > currentWindow) {
                        MonoBehaviour.print("Proximity sensor reset on " + this.vessel.name);
                        isArmed = true;
                    }
                    else if (!departing && Math.Abs((currentDistance - currentWindow) - meterDistance) > currentWindow) {
                        MonoBehaviour.print("Proximity sensor reset on " + this.vessel.name);
                        isArmed = true;
                    }
                }
            }
        }

        public override void OnUpdate() {
            //In order for physics to take effect on jettisoned parts, the staging event has to be fired from OnUpdate
            if (fireNextupdate && !justRegistered) {
                int groupToFire = 0; //AGX: need to send correct group
                if (AGXInterface.AGExtInstalled()) {
                    groupToFire = int.Parse(agxGroupType);
                }
                else {
                    groupToFire = int.Parse(group);
                }
                Helper.fireEvent(this.part, groupToFire, (int)agxGroupNum);
                fireNextupdate = false;
            }
        }

        public void Update() //AGX: The OnUpdate above only seems to run in flight mode, Update() here runs in all scenes
                {
            if (agxGroupType == "1" & groupLastUpdate != "1" || agxGroupType != "1" & groupLastUpdate == "1") //AGX: Monitor group to see if we need to refresh window
            {
                updateButtons();
                refreshPartWindow();
                if (agxGroupType == "1") {
                    groupLastUpdate = "1";
                }
                else {
                    groupLastUpdate = "0";
                }
            }
        }

        #endregion

        #region Methods

        private void updateButtons() {
            //Change to AGX buttons if AGX installed
            if (AGXInterface.AGExtInstalled()) {
                Fields["group"].guiActiveEditor = false;
                Fields["group"].guiActive = false;
                Fields["agxGroupType"].guiActiveEditor = true;
                Fields["agxGroupType"].guiActive = true;
                //Fields["agxGroupNum"].guiActiveEditor = true;
                //Fields["agxGroupNum"].guiActive = true;
                if (agxGroupType == "1") //only show groups select slider when selecting action group
                {
                    Fields["agxGroupNum"].guiActiveEditor = true;
                    Fields["agxGroupNum"].guiActive = true;
                    //Fields["agxGroupNum"].guiName = "Group:";
                }
                else {
                    Fields["agxGroupNum"].guiActiveEditor = false;
                    Fields["agxGroupNum"].guiActive = false;
                    //Fields["agxGroupNum"].guiName = "N/A";
                    //agxGroupNum = 1;
                }
            }
            else //AGX not installed, leave at default
            {
                Fields["group"].guiActiveEditor = true;
                Fields["group"].guiActive = true;
                Fields["agxGroupType"].guiActiveEditor = false;
                Fields["agxGroupType"].guiActive = false;
                Fields["agxGroupNum"].guiActiveEditor = false;
                Fields["agxGroupNum"].guiActive = false;
            }
        }

        private void updateDistance() {
            double testDistance = 0;
            double lastDistance = 0;
            ProxSensor closestSensor = null;

            //Set the last distance for the purpose of direction determination
            lastDistance = currentDistance;

            //Find closest target on our channel and set closestDistance
            foreach (var listener in ProxChannel.Listeners.ToList()) {
                if (this.vessel.id != listener.vessel.id) {
                    testDistance = Vector3d.Distance(this.vessel.GetWorldPos3D(), listener.vessel.GetWorldPos3D());
                    //Deregister listeners that are moving beyond unpack distance
                    if (testDistance >= 2500) {
                        MonoBehaviour.print(listener.vessel.vesselName + " is de-registering from proximity sensor at " + testDistance + " km.");
                        ProxChannel.Listeners.Remove(listener);
                    }
                    //Set distance and listener to the values from the closest non-self sensor on the same channel as us
                    else if (listener.channel == this.channel && (testDistance < currentDistance || closestSensor == null)) {
                        closestSensor = listener;
                        currentDistance = testDistance;
                    }
                }
            }

            //If no sensors detected, proceed no further
            if (closestSensor == null) {
                return;
            }

            //Determine if the vessel is approaching or departing
            departing = (currentDistance < lastDistance ? false : true);

            //Update target window size based on current velocity relative to target
            //If the target was just registered (AKA just entered 2.5km), it's velocity measurements are innacurate. Manually set to 0 until next pass.
            currentWindow = (closestSensor.justRegistered ? 0 : Math.Abs(currentDistance - lastDistance) * 1.05);
            //We now have one data point. Remove the justRegistered flag for the next pass
            if (closestSensor.justRegistered) {
                MonoBehaviour.print(closestSensor.vessel.name + " inelligible for proximity detection this time. Waiting for next pass in .02 seconds");
                closestSensor.justRegistered = false;
            }
        }

        public void clearListeners(GameScenes scene) {
            //On scene change, clear out all of the registered listeners
            ProxChannel.Listeners.Clear();
            ProxChannel.Listeners.TrimExcess();
            MonoBehaviour.print("OnSceneChange");
        }

        public void registerListener(Vessel ves) {
            if (ProxChannel.Listeners.Any(listener => listener.vessel.id == this.vessel.id) == true) {
                ProxChannel.Listeners.RemoveAll(listener => listener.vessel.id == this.vessel.id);
            }
            MonoBehaviour.print(this.vessel.vesselName + " proximity alarm has been registered");
            //Register sensor to proximity sensor list
            ProxChannel.Listeners.Add(this);
            justRegistered = true;
        }

        public void registerListener() {
            if (ProxChannel.Listeners.Any(listener => listener.vessel.id == this.vessel.id) == true) {
                ProxChannel.Listeners.RemoveAll(listener => listener.vessel.id == this.vessel.id);
            }
            MonoBehaviour.print(this.vessel.vesselName + " proximity alarm has been registered");
            //Register sensor to proximity sensor list
            ProxChannel.Listeners.Add(this);
            justRegistered = true;
        }

        private void refreshPartWindow() //AGX: Refresh right-click part window to show/hide Groups slider
        {
            UIPartActionWindow[] partWins = FindObjectsOfType<UIPartActionWindow>();
            //print("Wind count " + partWins.Count());
            foreach (UIPartActionWindow partWin in partWins) {
                partWin.displayDirty = true;
            }
        }

        private void OnDetach(bool first) {
            MonoBehaviour.print("OnDetach");
            //Remove prox sensor from listener list
            ProxChannel.Listeners.Remove(this);
        }

        private void OnJustAboutToBeDestroyed() {
            MonoBehaviour.print("OnJustAboutToBeDestroyed");
            //Remove prox sensor from listener list
            ProxChannel.Listeners.Remove(this);
        }

        private void OnEditorAttach() {
            RenderingManager.AddToPostDrawQueue(99, updateEditor);
        }

        private void OnEditorDetach() {

            RenderingManager.RemoveFromPostDrawQueue(99, updateEditor);
        }

        private void OnEditorDestroy() {
            RenderingManager.RemoveFromPostDrawQueue(99, updateEditor);
        }

        public void updateEditor() {
            //Update buttons
            updateButtons();
        }

        #endregion

    }
}
