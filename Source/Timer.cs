/*
 * Author: dtobi, Firov
 * This work is shared under Creative Commons CC BY-NC-SA 3.0 license.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

using KSP.UI.Screens;

namespace Lib
{
    public class Timer : PartModule
    {
        #region Fields

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
                "15",
                "16"
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
                "Abort",
                "Gear"
            }
        )]
        public string group = "0";

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
                "15",
                "16"
            },
            display = new String[] {
                "Stage",
                "Action Group:",
                "Lights",
                "RCS",
                "SAS",
                "Brakes",
                "Abort",
                "Gear"
            }
        )]
        public string agxGroupType = "0";

        // AGX Action groups, use own slider if selected, only show this field if AGXGroup above is 1
        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = false, guiName = "Group:", guiFormat = "N0"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = 1f, maxValue = 250f, incrementLarge = 75f, incrementSmall = 25f, incrementSlide = 1f)]
        public float agxGroupNum = 1;
        
        // remember the time wehen the countdown was started
        [KSPField(isPersistant = true, guiActive = false)]
        private double triggerTime = 0;

        // Delay in seconds. Used for precise measurement
        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = false, guiName = "Seconds", guiFormat = "F2", guiUnits = "sec"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = 0f, maxValue = 120f, incrementLarge = 20f, incrementSmall = 1f, incrementSlide = .05f)]
        public float triggerDelaySeconds = 0;

        // Delay in minutes. Used for longer term measurement
        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = false, guiName = "Minutes", guiFormat = "F2", guiUnits = "min"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = 0f, maxValue = 360f, incrementLarge = 60f, incrementSmall = 5f, incrementSlide = .25f)]
        public float triggerDelayMinutes = 0;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Remaining Time", guiFormat = "F2")]
        private double remainingTime = 0;

        [KSPField(isPersistant = true)]
        private Boolean allowStage = true;

        [KSPField(isPersistant = true)]
        private Boolean useSeconds = true;

        [KSPField(isPersistant = true)]
        private Boolean armed = true;

        #endregion


        #region Events

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Use Seconds")]
        public void setSeconds() {
            useSeconds = true;
            updateButtons();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Use Minutes")]
        public void setMinutes() {
            useSeconds = false;
            updateButtons();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Enable Staging")]
        public void activateStaging() {
            enableStaging();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Disable Staging")]
        public void deactivateStaging() {
            disableStaging();
        }

        [KSPEvent(guiName = "Start Countdown", guiActive = true)]
        public void activateTimer() {
            reset();
            setTimer();
        }

        [KSPAction("Start Countdown")]
        public void activateTimerAG(KSPActionParam param) {
            reset();
            setTimer();
        }

        [KSPEvent(guiName = "Reset", guiActive = true)]
        public void resetTimer() {
            reset();
        }

        [KSPAction("Reset")]
        public void resetTimerAG(KSPActionParam param) {
            reset();
        }

        #endregion


        #region Variables

        private int previousStage = 0;
        private string groupLastUpdate = "0"; //AGX: What was our selected group last update frame? Top slider.
        

        #endregion


        #region Overrides

        public override void OnStart(StartState state) {
            if(!armed){
                Utility.switchLight(this.part, "light-go", true);
                Utility.playAnimationSetToPosition(this.part, "glow", 1);
                this.part.stackIcon.SetIconColor(XKCDColors.Red);
            }
            if (allowStage) {
                Events["activateStaging"].guiActiveEditor = false;
                Events["deactivateStaging"].guiActiveEditor = true;
            }
            else {
                Invoke("disableStaging", 0.25f);
            }
            GameEvents.onVesselChange.Add(onVesselChange);
            part.ActivatesEvenIfDisconnected = true;
            //Initial button layout
            updateButtons();
        }

        public override void OnActive() {
            //If staging enabled, set timer
            if (allowStage && armed) {
                setTimer();
            }
        }

        public override void OnUpdate() {
            //Check to see if the timer has been dragged in the staging list. If so, reset icon color
            if (this.part.inverseStage != previousStage && allowStage && !armed && this.part.inverseStage + 1 < StageManager.CurrentStage) {
                reset();
            }
            previousStage = this.part.inverseStage;

            //If the timer has been activated, start the countdown, activate the model's LED, and change the icon color
            if (triggerTime > 0 && armed) {
                remainingTime = triggerTime + (useSeconds ? triggerDelaySeconds : triggerDelayMinutes * 60) - Planetarium.GetUniversalTime();
                Utility.switchLight(this.part, "light-go", true);
                Utility.playAnimationSetToPosition(this.part, "glow", 1);
                this.part.stackIcon.SetIconColor(XKCDColors.BrightYellow);

                //Once the timer hits 0 activate the stage/AG, disable the model's LED, and change the icon color
                if (remainingTime < 0) {
                    print("Stage:" + Helper.KM_dictAGNames[int.Parse(group)]);
                    int groupToFire = 0; //AGX: need to send correct group
                    if(AGXInterface.AGExtInstalled())
                    {
                        groupToFire = int.Parse(agxGroupType);
                    }
                    else
                    {
                        groupToFire = int.Parse(group);
                    }
                    Helper.fireEvent(this.part, groupToFire, (int)agxGroupNum);
                    this.part.stackIcon.SetIconColor(XKCDColors.Red);
                    triggerTime = 0;
                    remainingTime = 0;
                    //Disable timer until reset
                    armed = false;
                }
            }
        }
            public void Update() //AGX: The OnUpdate above only seems to run in flight mode, Update() here runs in all scenes
            {
                if (agxGroupType == "1" & groupLastUpdate != "1" || agxGroupType != "1" & groupLastUpdate == "1") //AGX: Monitor group to see if we need to refresh window
                {
                updateButtons();
                refreshPartWindow();
                if (agxGroupType == "1")
                {
                    groupLastUpdate = "1";
                }
                else
                {
                    groupLastUpdate = "0";
                }
                }
            }

        #endregion


        #region Methods


        public void onVesselChange(Vessel newVessel) {
            if (newVessel == this.vessel && !allowStage) {
                Invoke("disableStaging", 0.25f);
            }
        }

        private void enableStaging() {
            part.stackIcon.CreateIcon();
            StageManager.Instance.SortIcons(true);
            allowStage = true;

            //Toggle button visibility so currently inactive mode's button is visible
            Events["activateStaging"].guiActiveEditor = false;
            Events["deactivateStaging"].guiActiveEditor = true;
        }

        private void disableStaging() {
            part.stackIcon.RemoveIcon();
            StageManager.Instance.SortIcons(true);
            allowStage = false;

            //Toggle button visibility so currently inactive mode's button is visible
            Events["activateStaging"].guiActiveEditor = true;
            Events["deactivateStaging"].guiActiveEditor = false;
        }

        private void setTimer() {
            if (armed) {
                //Set the trigger time, which will be caught in OnUpdate
                triggerTime = Planetarium.GetUniversalTime();
                print("Activating Timer: " + (useSeconds ? triggerDelaySeconds : triggerDelayMinutes * 60));
            }
        }

        private void reset() {
            print("Timer reset");
            //Reset trigger and remaining time to 0
            triggerTime = 0;
            remainingTime = 0;
            //Switch off model lights
            Utility.switchLight(this.part, "light-go", false);
            Utility.playAnimationSetToPosition(this.part, "glow", 0);
            //Reset icon color to white
            this.part.stackIcon.SetIconColor(XKCDColors.White);
            //Reset armed variable
            armed = true;
            //Reset activation status on part
            this.part.deactivate();
        }

        private void updateButtons() {
            if (useSeconds)
            {
                //Show minute button
                Events["setMinutes"].guiActiveEditor = true;
                Events["setMinutes"].guiActive = true;
                //Hide minute scale
                Fields["triggerDelayMinutes"].guiActiveEditor = false;
                Fields["triggerDelayMinutes"].guiActive = false;
                //Hide seconds button
                Events["setSeconds"].guiActiveEditor = false;
                Events["setSeconds"].guiActive = false;
                //Show seconds scale
                Fields["triggerDelaySeconds"].guiActiveEditor = true;
                Fields["triggerDelaySeconds"].guiActive = true;
                //Reset minute scale
                triggerDelayMinutes = 0f;
            }
            else
            {
                //Hide minute button
                Events["setMinutes"].guiActiveEditor = false;
                Events["setMinutes"].guiActive = false;
                //Show minute scale
                Fields["triggerDelayMinutes"].guiActiveEditor = true;
                Fields["triggerDelayMinutes"].guiActive = true;
                //Show seconds button
                Events["setSeconds"].guiActiveEditor = true;
                Events["setSeconds"].guiActive = true;
                //Hide seconds scale
                Fields["triggerDelaySeconds"].guiActiveEditor = false;
                Fields["triggerDelaySeconds"].guiActive = false;
                //Reset seconds scale
                triggerDelaySeconds = 0;
            }

            //Change to AGX buttons if AGX installed
            if (AGXInterface.AGExtInstalled())
            {
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
                else
                {
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

        private void onGUI() {
            //Update Buttons
            updateButtons();
        }

        private void refreshPartWindow() //AGX: Refresh right-click part window to show/hide Groups slider
        {
            UIPartActionWindow[] partWins = FindObjectsOfType<UIPartActionWindow>();
            //print("Wind count " + partWins.Count());
            foreach(UIPartActionWindow partWin in partWins)
            {
                partWin.displayDirty = true;
            }
        }

        #endregion
    }
}
