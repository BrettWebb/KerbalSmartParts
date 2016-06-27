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

namespace Lib
{
    public class Altimeter : PartModule
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

        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = false, guiName = "Meters", guiFormat = "F0", guiUnits = "m"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = 0f, maxValue = 1000f, incrementLarge = 200f, incrementSmall = 25f, incrementSlide = 1f)]
        public float meterHeight = 0;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = false, guiName = "Kilometers", guiFormat = "F0", guiUnits = "km"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = 0f, maxValue = 500f, incrementLarge = 100f, incrementSmall = 25f, incrementSlide = 1f)]
        public float kilometerHeight = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Active"),
            UI_Toggle(disabledText = "False", enabledText = "True")]
        public bool isArmed = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Trigger on"),
            UI_ChooseOption(options = new string[] { "All", "Ascent", "Descent" })]
        public string direction = "All";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Auto Reset"),
            UI_Toggle(disabledText = "False", enabledText = "True")]
        public bool autoReset = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Use AGL"),
            UI_Toggle(disabledText = "False", enabledText = "True")]
        public bool useAGL = true;

        [KSPField(isPersistant = true)]
        public bool useKilometer = false;

        #endregion


        #region Events

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Use Kilometers")]
        public void useKilometers() {
            useKilometer = true;
            updateButtons();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Use Meters")]
        public void useMeters() {
            useKilometer = false;
            updateButtons();
        }

        [KSPAction("Activate Detection")]
        public void doActivateAG(KSPActionParam param) {
            isArmed = true;
        }

        [KSPAction("Deactivate Detection")]
        public void doDeActivateAG(KSPActionParam param) {
            isArmed = false;
        }

        #endregion


        #region Variables

        private double alt = 0;
        private double currentWindow = 0;
        private Boolean illuminated = false;
        private Boolean ascending = false;
        private Boolean fireNextupdate = false;
        private string groupLastUpdate = "0"; //AGX: What was our selected group last update frame? Top slider.

        #endregion


        #region Overrides

        public override void OnStart(StartState state) {
            //Initial button layout
            updateButtons();
            //Force activation no matter which stage it's on
            this.part.force_activate();
            print("KM Altimeter Detector Started");
            updateButtons();
        }

        public override void OnUpdate() {
            //Check to see if the device has been rearmed, if so, deactivate the lights
            if (isArmed && illuminated) {
                lightsOff();
            }
            //In order for physics to take effect on jettisoned parts, the staging event has to be fired from OnUpdate
            if (fireNextupdate) {
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

        public override void OnFixedUpdate() {
            //Check current altitude
            updateAltitude();

            //If the device is armed, check for the trigger altitude
            if (isArmed) {
                //We're ascending. Trigger at or above target height
                if (direction != "Descent" && ascending && Math.Abs((alt - currentWindow) - (useKilometer ? kilometerHeight * 1000 : meterHeight)) < currentWindow) {
                    //This flag is checked for in OnUpdate to trigger staging
                    fireNextupdate = true;
                    lightsOn();
                    isArmed = false;
                }
                //We're descending. Trigger at or below target height
                else if (direction != "Ascent" && !ascending && Math.Abs((alt + currentWindow) - (useKilometer ? kilometerHeight * 1000 : meterHeight)) < currentWindow) {
                    //This flag is checked for in OnUpdate to trigger staging
                    fireNextupdate = true;
                    lightsOn();
                    isArmed = false;
                }
            }

            //If auto reset is enabled, wait for departure from the target window and rearm
            if (!isArmed & autoReset) {
                if (ascending && Math.Abs((alt - currentWindow) - (useKilometer ? kilometerHeight * 1000 : meterHeight)) > currentWindow) {
                    isArmed = true;
                }
                else if (!ascending && Math.Abs((alt + currentWindow) - (useKilometer ? kilometerHeight * 1000 : meterHeight)) > currentWindow) {
                    isArmed = true;
                }
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

        private void refreshPartWindow() //AGX: Refresh right-click part window to show/hide Groups slider
        {
            UIPartActionWindow[] partWins = FindObjectsOfType<UIPartActionWindow>();
            //print("Wind count " + partWins.Count());
            foreach (UIPartActionWindow partWin in partWins) {
                partWin.displayDirty = true;
            }
        }

        #endregion


        #region Methods

        private void updateAltitude() {
            //Sea altitude
            double altSea = this.vessel.mainBody.GetAltitude(this.vessel.CoM);
            //Altitude over terrain. Does not factor in ocean surface.
            double altSurface = altSea - this.vessel.terrainAltitude;
            //Set the last altitude for the purpose of direction determination
            double lastAlt = alt;
            //Set current altitude
            if(!this.vessel.mainBody.ocean) {
                //If the cellestial body this craft is orbiting lacks an ocean, always use AGL
                alt = altSurface;
            }            
            else if (useAGL) {
                //If the planet has an ocean, and the "useAGL" is selected, calculate whether AGL or ASL is closer, and use that
                alt = (altSurface < altSea ? altSurface : altSea);
            }
            //Otherwise, use sea level
            else {                
                alt = altSea;
            }
            //Determine if the vessel is ascending or descending
            ascending = (lastAlt < alt ? true : false);
            //Update target window size based on current vertical velocity
            currentWindow = Math.Abs((TimeWarp.fixedDeltaTime * this.vessel.verticalSpeed) * 1.05);
        }

        private void lightsOn() {
            //Switch off model lights
            Utility.switchLight(this.part, "light-go", true);
            Utility.playAnimationSetToPosition(this.part, "glow", 1);
            illuminated = true;
        }

        private void lightsOff() {
            //Switch off model lights
            Utility.switchLight(this.part, "light-go", false);
            Utility.playAnimationSetToPosition(this.part, "glow", 0);
            illuminated = false;
        }

        private void updateButtons() {
            if (useKilometer) {
                //Show meter button
                Events["useMeters"].guiActiveEditor = true;
                Events["useMeters"].guiActive = true;
                //Hide meter scale
                Fields["meterHeight"].guiActiveEditor = false;
                Fields["meterHeight"].guiActive = false;
                //Hide kilometer button
                Events["useKilometers"].guiActiveEditor = false;
                Events["useKilometers"].guiActive = false;
                //Show kilometer scale
                Fields["kilometerHeight"].guiActiveEditor = true;
                Fields["kilometerHeight"].guiActive = true;
                //Reset meter scale
                meterHeight = 0;
            }
            else {
                //Hide meter button
                Events["useMeters"].guiActiveEditor = false;
                Events["useMeters"].guiActive = false;
                //Show meter scale
                Fields["meterHeight"].guiActiveEditor = true;
                Fields["meterHeight"].guiActive = true;
                //Show kilometer button
                Events["useKilometers"].guiActiveEditor = true;
                Events["useKilometers"].guiActive = true;
                //Hide kilometer scale
                Fields["kilometerHeight"].guiActiveEditor = false;
                Fields["kilometerHeight"].guiActive = false;
                //Reset kilometer scale
                kilometerHeight = 0;
            }

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

		private void onGUI() {
            //Update buttons
            updateButtons();
        }

        #endregion
    }
}

