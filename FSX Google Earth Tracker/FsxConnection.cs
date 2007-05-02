using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.FlightSimulator.SimConnect;
using System.Timers;
using System.Runtime.InteropServices;
using System.Xml;

namespace Fsxget
{
    public class FsxConnection
    {
        
        public class ObjectData<T>
        {
            private bool bModified;
            private T tValue;

            public ObjectData()
            {
                tValue = default(T);
                bModified = false;
            }

            public T Value
            {
                get
                {
                    bModified = false;
                    return tValue;
                }
                set
                {
                    if ((tValue == null && value != null) || !tValue.Equals(value))
                    {
                        bModified = true;
                        tValue = value;
                    }
                }
            }

            public bool IsModified
            {
                get
                {
                    return bModified;
                }
            }
        }
        public class ObjectPosition
        {
            private ObjectData<double> dLon;
            private ObjectData<double> dLat;
            private ObjectData<double> dAlt;
            private double dTime;

            public ObjectPosition()
            {
                dLon = new ObjectData<double>();
                dLat = new ObjectData<double>();
                dAlt = new ObjectData<double>();
            }

            public ObjectData<double> Longitude
            {
                get
                {
                    return dLon;
                }
            }
            public ObjectData<double> Latitude
            {
                get
                {
                    return dLat;
                }
            }
            public ObjectData<double> Altitude
            {
                get
                {
                    return dAlt;
                }
            }
            public double Time
            {
                get
                {
                    return dTime;
                }
                set
                {
                    dTime = value;
                }
            }
            public bool HasMoved
            {
                get
                {
                    return (Longitude.IsModified || Latitude.IsModified || Altitude.IsModified);
                }
            }
            public String Coordinate
            {
                get
                {
                    return XmlConvert.ToString(dLon.Value) + "," + XmlConvert.ToString(dLat.Value) + "," + XmlConvert.ToString(dAlt.Value);
                }
            }
        }
        public class SceneryObject
        {
            public enum STATE
            {
                NEW,
                UNCHANGED,
                MODIFIED,
                DELETED,
                DATAREAD,
            }

            protected STATE tState;
            protected DATA_REQUESTS tType;
            protected uint unID;

            public SceneryObject(uint unID, DATA_REQUESTS tType)
            {
                tState = STATE.NEW;
                this.tType = tType;
                this.unID = unID;
            }
        
            public STATE ObjectState
            {
                get
                {
                    return tState;
                }
                set
                {
                    tState = value;
                }
            }
            public uint ObjectID
            {
                get
                {
                    return unID;
                }
            }
            public DATA_REQUESTS ObjectType
            {
                get
                {
                    return tType;
                }
            }
        }
        public class SceneryMovingObject : SceneryObject
        {
            public class ObjectPath
            {
                protected String strCoordinates;
                STATE tState;

                public ObjectPath()
                {
                    strCoordinates = "";
                    tState = STATE.NEW;
                }

                public ObjectPath( ref StructBasicMovingSceneryObject obj )
                {
                    tState = STATE.NEW;
                    AddPosition(ref obj);
                }

                public void AddPosition( ref StructBasicMovingSceneryObject obj )
                {
                    strCoordinates += XmlConvert.ToString(obj.dLongitude) + "," + XmlConvert.ToString(obj.dLatitude) + "," + XmlConvert.ToString(obj.dAltitude) + " ";
                    if (tState == STATE.DATAREAD)
                        tState = STATE.MODIFIED;
                }

                public void Clear()
                {
                    strCoordinates = "";
                    if (tState == STATE.DATAREAD)
                        tState = STATE.MODIFIED;
                }

                public STATE State
                {
                    get
                    {
                        return tState;
                    }
                    set
                    {
                        tState = value;
                    }
                }

                public String Coordinates
                {
                    get
                    {
                        return strCoordinates;
                    }
                }
            }
            public class PathPrediction
            {
                protected bool bPredictionPoints;
                public ObjectPosition[] positions;
                protected double dTimeElapsed;
                STATE tState;

                public PathPrediction(bool bWithPoints)
                {
                    tState = STATE.NEW;
                    HasPoints = bWithPoints;
                    dTimeElapsed = 0;
                }

                public void Update(ref StructBasicMovingSceneryObject obj)
                {
                    dTimeElapsed = obj.dTime - positions[0].Time;
                    if (dTimeElapsed > 0)
                    {
                        for (int i = 1; i < positions.Length; i++)
                        {
                            CalcPositionByTime(ref obj, ref positions[i]);
                        }
                        positions[0].Longitude.Value = obj.dLongitude;
                        positions[0].Latitude.Value = obj.dLatitude;
                        positions[0].Altitude.Value = obj.dAltitude;
                        positions[0].Time = obj.dTime;

                        if (tState == STATE.DATAREAD)
                        {
                            if (positions[0].HasMoved)
                                tState = STATE.MODIFIED;
                            else
                                tState = STATE.UNCHANGED;
                        }
                    }
                }

                private void CalcPositionByTime(ref StructBasicMovingSceneryObject objNew, ref ObjectPosition tResultPos)
                {
                    double dScale = tResultPos.Time / dTimeElapsed;

                    tResultPos.Latitude.Value = objNew.dLatitude + dScale * (objNew.dLatitude - positions[0].Latitude.Value);
                    tResultPos.Longitude.Value = objNew.dLongitude + dScale * (objNew.dLongitude - positions[0].Longitude.Value);
                    tResultPos.Altitude.Value = objNew.dAltitude + dScale * (objNew.dAltitude - positions[0].Altitude.Value);
                }

                public bool HasPoints
                {
                    get
                    {
                        return bPredictionPoints;
                    }
                    set
                    {
                        if( bPredictionPoints != value )    
                        {
                            bPredictionPoints = value;
                            SettingsList lstPoint = (SettingsList)Program.Config[Config.SETTING.PREDICTION_POINTS];
                            if (bPredictionPoints)
                            {
                                positions = new ObjectPosition[lstPoint.listSettings.Count + 1];
                                for (int i = 0; i < positions.Length; i++)
                                {
                                    positions[i] = new ObjectPosition();
                                    if( i > 0 )
                                        positions[i].Time = (double)lstPoint["Time", i - 1].IntValue;
                                }
                            }
                        }
                        if (!bPredictionPoints && positions == null)
                        {
                            positions = new ObjectPosition[2];
                            positions[0] = new ObjectPosition();
                            positions[1] = new ObjectPosition();
                            positions[1].Time = 1200;
                        }
                        positions[0].Time = 0;
                    }
                }
                public ObjectPosition[] Positions
                {
                    get
                    {
                        return positions;
                    }
                }
                
                public STATE State
                {
                    get
                    {
                        return tState;
                    }
                    set
                    {
                        tState = value;
                    }
                }
            }

            #region Variables
            protected ObjectData<String> strTitle;
            protected ObjectData<String> strATCType;
            protected ObjectData<String> strATCModel;
            protected ObjectData<String> strATCID;
            protected ObjectData<String> strATCAirline;
            protected ObjectData<String> strATCFlightNumber;
            protected ObjectPosition objPos;
            protected ObjectData<double> dHeading;
            protected double dTime;
            public ObjectPath objPath;
            public PathPrediction pathPrediction;
            #endregion

            public SceneryMovingObject(uint unID, DATA_REQUESTS tType, ref StructBasicMovingSceneryObject obj)
                : base( unID, tType )
            {
                strTitle = new ObjectData<String>();
                strATCType = new ObjectData<String>();
                strATCModel = new ObjectData<String>();
                strATCID = new ObjectData<String>();
                strATCAirline = new ObjectData<String>();
                strATCFlightNumber = new ObjectData<String>();
                objPos = new ObjectPosition();
                objPath = new ObjectPath(ref obj);
                dHeading = new ObjectData<double>();
                strTitle.Value = obj.szTitle;
                strATCType.Value = obj.szATCType;
                strATCModel.Value = obj.szATCModel;
                strATCID.Value = obj.szATCID;
                strATCAirline.Value = obj.szATCAirline;
                strATCFlightNumber.Value = obj.szATCFlightNumber;
                objPos.Longitude.Value = obj.dLongitude;
                objPos.Latitude.Value = obj.dLatitude;
                objPos.Altitude.Value = obj.dAltitude;
                dHeading.Value = obj.dHeading;
                dTime = obj.dTime;
                ConfigChanged();
            }

            public void Update(ref StructBasicMovingSceneryObject obj)
            {
                if (tState == STATE.DELETED)
                    return;
                if (obj.dTime != dTime && pathPrediction != null)
                {
                    pathPrediction.Update( ref obj );
                }
                if (objPath != null && (obj.dLongitude != objPos.Longitude.Value || obj.dLatitude != objPos.Latitude.Value || obj.dAltitude != objPos.Altitude.Value))
                {
                    objPath.AddPosition(ref obj);
                }

                objPos.Longitude.Value = obj.dLongitude;
                objPos.Latitude.Value = obj.dLatitude;
                objPos.Altitude.Value = obj.dAltitude;
                strTitle.Value = obj.szTitle;
                strATCType.Value = obj.szATCType;
                strATCModel.Value = obj.szATCModel;
                strATCID.Value = obj.szATCID;
                strATCAirline.Value = obj.szATCAirline;
                strATCFlightNumber.Value = obj.szATCFlightNumber;
                dHeading.Value = obj.dHeading;
                dTime = obj.dTime;
                if (tState == STATE.DATAREAD)
                {
                    if (HasMoved || HasChanged)
                        tState = STATE.MODIFIED;
                    else
                        tState = STATE.UNCHANGED;
                }
            }

            public void ConfigChanged()
            {
                bool bPath = false;
                bool bPrediction = false;
                bool bPredictionPoints = false;
                switch (tType)
                {
                    case DATA_REQUESTS.REQUEST_USER_AIRCRAFT:
                        bPrediction = Program.Config[Config.SETTING.USER_PATH_PREDICTION]["Enabled"].BoolValue;
                        bPredictionPoints = true;
                        bPath = Program.Config[Config.SETTING.QUERY_USER_PATH]["Enabled"].BoolValue;
                        break;
                    case DATA_REQUESTS.REQUEST_AI_PLANE:
                        bPrediction = Program.Config[Config.SETTING.QUERY_AI_AIRCRAFTS]["Prediction"].BoolValue;
                        bPredictionPoints = Program.Config[Config.SETTING.QUERY_AI_AIRCRAFTS]["PredictionPoints"].BoolValue;
                        break;
                    case DATA_REQUESTS.REQUEST_AI_HELICOPTER:
                        bPrediction = Program.Config[Config.SETTING.QUERY_AI_HELICOPTERS]["Prediction"].BoolValue;
                        bPredictionPoints = Program.Config[Config.SETTING.QUERY_AI_HELICOPTERS]["PredictionPoints"].BoolValue;
                        break;
                    case DATA_REQUESTS.REQUEST_AI_BOAT:
                        bPrediction = Program.Config[Config.SETTING.QUERY_AI_BOATS]["Prediction"].BoolValue;
                        bPredictionPoints = Program.Config[Config.SETTING.QUERY_AI_BOATS]["PredictionPoints"].BoolValue;
                        break;
                    case DATA_REQUESTS.REQUEST_AI_GROUND:
                        bPrediction = Program.Config[Config.SETTING.QUERY_AI_GROUND_UNITS]["Prediction"].BoolValue;
                        bPredictionPoints = Program.Config[Config.SETTING.QUERY_AI_GROUND_UNITS]["PredictionPoints"].BoolValue;
                        break;
                }
                if (bPath && objPath == null)
                    objPath = new ObjectPath();
                else if (!bPath)
                    objPath = null;

                if (bPrediction)
                {
                    if (pathPrediction == null)
                        pathPrediction = new PathPrediction(bPredictionPoints);
                    else
                        pathPrediction.HasPoints = bPredictionPoints;
                }
                else
                {
                    pathPrediction = null;
                }
            }

            public void ReplaceObjectInfos(ref String str)
            {
                str = str.Replace("%TITLE%", Title.Value);
                str = str.Replace("%ATCTYPE%", ATCType.Value);
                str = str.Replace("%ATCMODEL%", ATCModel.Value);
                str = str.Replace("%ATCID%", ATCID.Value);
                str = str.Replace("%ATCFLIGHTNUMBER%", ATCFlightNumber.Value);
                str = str.Replace("%ATCAIRLINE%", ATCAirline.Value);
                str = str.Replace("%LONGITUDE%", XmlConvert.ToString(ObjectPosition.Longitude.Value));
                str = str.Replace("%LATITUDE%", XmlConvert.ToString(ObjectPosition.Latitude.Value));
                str = str.Replace("%ALTITUDE%", XmlConvert.ToString(ObjectPosition.Altitude.Value));
                str = str.Replace("%HEADING%", XmlConvert.ToString(Heading.Value));
                str = str.Replace("%IMAGE%", Program.Config.Server + "/" + Title.Value);
            }

            #region Accessors
            public ObjectData<String> Title
            {
                get
                {
                    return strTitle;
                }
            }

            public ObjectData<String> ATCType
            {
                get
                {
                    return strATCType;
                }
            }

            public ObjectData<String> ATCModel
            {
                get
                {
                    return strATCModel;
                }
            }
            
            public ObjectData<String> ATCID
            {
                get
                {
                    return strATCID;
                }
            }

            public ObjectData<String> ATCAirline
            {
                get
                {
                    return strATCAirline;
                }
            }

            public ObjectData<String> ATCFlightNumber
            {
                get
                {
                    return strATCFlightNumber;
                }
            }

            public ObjectPosition ObjectPosition
            {
                get
                {
                    return objPos;
                }
            }

            public ObjectData<double> Heading
            {
                get
                {
                    return dHeading;
                }
            }

            public String Coordinates
            {
                get
                {
                    return XmlConvert.ToString(objPos.Longitude.Value) + "," + XmlConvert.ToString(objPos.Latitude.Value) + "," + XmlConvert.ToString(objPos.Altitude.Value);
                }
            }
            
            public double Time
            {
                get
                {
                    return dTime;
                }
            }

            public STATE State
            {
                get
                {
                    return tState;
                }
                set
                {
                    tState = value;
                }
            }

            public bool HasMoved
            {
                get
                {
                    return objPos.HasMoved || dHeading.IsModified;
                }
            }
            
            public bool HasChanged
            {
                get
                {
                    if( strTitle.IsModified || 
                        strATCType.IsModified ||
                        strATCModel.IsModified ||
                        strATCID.IsModified ||
                        strATCAirline.IsModified ||
                        strATCFlightNumber.IsModified )
                        return true;
                    else
                        return false;
                }
            }

            #endregion
        }

        #region Variables
        private FsxetForm frmMain;
        private IntPtr frmMainHandle;
        private const int WM_USER_SIMCONNECT = 0x0402;
        private Timer timerConnect;
        private Timer timerQueryUserAircraft;
        public SceneryMovingObject objUserAircraft;
        public Object lockUserAircraft;
        private uint uiUserAircraftID;
        private SimConnect simconnect;
        public Object lockSimConnect;
        public StructObjectContainer objAIAircrafts;
        public StructObjectContainer objAIHelicopters;
        public StructObjectContainer objAIBoats;
        public StructObjectContainer objAIGroundUnits;
        #endregion

        #region Structs & Enums
        enum EVENT_ID
        {
            EVENT_MENU,
            EVENT_MENU_START,
            EVENT_MENU_STOP,
            EVENT_MENU_OPTIONS,
            EVENT_MENU_CLEAR_USER_PATH
        };
        
        enum DEFINITIONS
        {
            StructBasicMovingSceneryObject,
        };

        public enum DATA_REQUESTS
        {
            REQUEST_USER_AIRCRAFT,
            REQUEST_AI_HELICOPTER,
            REQUEST_AI_PLANE,
            REQUEST_AI_BOAT,
            REQUEST_AI_GROUND,
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct StructBasicMovingSceneryObject
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String szTitle;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public String szATCType;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public String szATCModel;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public String szATCID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public String szATCAirline;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public String szATCFlightNumber;
            public double dLatitude;
            public double dLongitude;
            public double dAltitude;
            public double dTime;
            public double dHeading;
        };

        public struct StructObjectContainer
        {
            public Object lockObject;
            public Hashtable htObjects;
            public Timer timer;
        }
        #endregion

        #region Construction
        public FsxConnection(FsxetForm frmMain, bool bAddOn)
        {
            this.frmMain = frmMain;
            this.frmMainHandle = frmMain.Handle;
            simconnect = null;
            if (bAddOn)
            {
                if (openConnection())
                {
                    AddMenuItems();
                }
            }
            else
            {
                timerConnect = new Timer();
                timerConnect.Interval = 3000;
                timerConnect.Elapsed += new ElapsedEventHandler(OnTimerConnectElapsed);
                timerConnect.Start();
            }

            objAIAircrafts = new StructObjectContainer();
            objAIAircrafts.lockObject = new Object();
            objAIAircrafts.htObjects = new Hashtable();
            objAIAircrafts.timer = new Timer();
            objAIAircrafts.timer.Elapsed += new ElapsedEventHandler(OnTimerQueryAIAircraftsElapsed);

            objAIHelicopters = new StructObjectContainer();
            objAIHelicopters.lockObject = new Object();
            objAIHelicopters.htObjects = new Hashtable();
            objAIHelicopters.timer = new Timer();
            objAIHelicopters.timer.Elapsed += new ElapsedEventHandler(OnTimerQueryAIHelicoptersElapsed);

            objAIBoats= new StructObjectContainer();
            objAIBoats.lockObject = new Object();
            objAIBoats.htObjects = new Hashtable();
            objAIBoats.timer = new Timer();
            objAIBoats.timer.Elapsed += new ElapsedEventHandler(OntimerQueryAIBoatsElapsed);

            objAIGroundUnits = new StructObjectContainer();
            objAIGroundUnits.lockObject = new Object();
            objAIGroundUnits.htObjects = new Hashtable();
            objAIGroundUnits.timer = new Timer();
            objAIGroundUnits.timer.Elapsed += new ElapsedEventHandler(OnTimerQueryAIGroundUnitsElapsed);
            
            timerQueryUserAircraft = new Timer();
            timerQueryUserAircraft.Elapsed += new ElapsedEventHandler(OnTimerQueryUserAircraftElapsed);

            lockUserAircraft = new Object();
            lockSimConnect = new Object();
        }
        public void InitializeTimers()
        {
            bool bQueryAI = Program.Config[Config.SETTING.QUERY_AI_OBJECTS]["Enabled"].BoolValue;

            timerQueryUserAircraft.Stop();
            timerQueryUserAircraft.Interval = Program.Config[Config.SETTING.QUERY_USER_AIRCRAFT]["Interval"].IntValue;
            timerQueryUserAircraft.Enabled = Program.Config[Config.SETTING.QUERY_USER_AIRCRAFT]["Enabled"].BoolValue;

            objAIAircrafts.timer.Stop();
            objAIAircrafts.timer.Interval = Program.Config[Config.SETTING.QUERY_AI_AIRCRAFTS]["Interval"].IntValue;
            objAIAircrafts.timer.Enabled = bQueryAI && Program.Config[Config.SETTING.QUERY_AI_AIRCRAFTS]["Enabled"].BoolValue;

            objAIHelicopters.timer.Stop();
            objAIHelicopters.timer.Interval = Program.Config[Config.SETTING.QUERY_AI_HELICOPTERS]["Interval"].IntValue;
            objAIHelicopters.timer.Enabled = bQueryAI && Program.Config[Config.SETTING.QUERY_AI_HELICOPTERS]["Enabled"].BoolValue;

            objAIBoats.timer.Stop();
            objAIBoats.timer.Interval = Program.Config[Config.SETTING.QUERY_AI_BOATS]["Interval"].IntValue;
            objAIBoats.timer.Enabled = bQueryAI && Program.Config[Config.SETTING.QUERY_AI_BOATS]["Enabled"].BoolValue;

            objAIGroundUnits.timer.Stop();
            objAIGroundUnits.timer.Interval = Program.Config[Config.SETTING.QUERY_AI_GROUND_UNITS]["Interval"].IntValue;
            objAIGroundUnits.timer.Enabled = bQueryAI && Program.Config[Config.SETTING.QUERY_AI_GROUND_UNITS]["Enabled"].BoolValue;
        }
        #endregion

        #region FSX-Handling
        private bool openConnection()
        {
            if (simconnect == null)
            {
                try
                {
                    simconnect = new SimConnect(frmMain.Text, frmMainHandle, WM_USER_SIMCONNECT, null, 0);
                    if (initDataRequest())
                    {
                        InitializeTimers();
                        return true;
                    }
                    else
                        return false;
                }
                catch
                {
                    return false;
                }
            }
            else
                return false;
        }
        private void closeConnection()
        {
            if (simconnect != null)
            {
                timerQueryUserAircraft.Stop();
                objAIAircrafts.timer.Stop();
                objAIHelicopters.timer.Stop();
                objAIBoats.timer.Stop();
                objAIGroundUnits.timer.Stop();
                
                simconnect.Dispose();
                simconnect = null;
            }
        }
        private bool initDataRequest()
        {
            try
            {
                // listen to connect and quit msgs
                simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
                simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);
                simconnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(simconnect_OnRecvEvent);

                // listen to exceptions
                simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

                // define a data structure
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "ATC Type", null, SIMCONNECT_DATATYPE.STRING32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "ATC Model", null, SIMCONNECT_DATATYPE.STRING32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "ATC ID", null, SIMCONNECT_DATATYPE.STRING32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "ATC Airline", null, SIMCONNECT_DATATYPE.STRING64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "ATC Flight Number", null, SIMCONNECT_DATATYPE.STRING32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "Plane Altitude", "meters", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "Absolute Time", "seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.StructBasicMovingSceneryObject, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                // IMPORTANT: register it with the simconnect managed wrapper marshaller
                // if you skip this step, you will only receive a uint in the .dwData field.
                simconnect.RegisterDataDefineStruct<StructBasicMovingSceneryObject>(DEFINITIONS.StructBasicMovingSceneryObject);

                // catch a simobject data request
                simconnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(simconnect_OnRecvSimobjectDataBytype);

                return true;
            }
            catch (COMException ex)
            {
                frmMain.NotifyError("FSX Exception!\n\n" + ex.Message);
                return false;
            }
        }
        private void AddMenuItems()
        {
            if (simconnect != null)
            {
                try
                {
                    simconnect.MenuAddItem(frmMain.Text, EVENT_ID.EVENT_MENU, 0);
                    simconnect.MenuAddSubItem(EVENT_ID.EVENT_MENU, "&Start", EVENT_ID.EVENT_MENU_START, 0);
                    simconnect.MenuAddSubItem(EVENT_ID.EVENT_MENU, "Sto&p", EVENT_ID.EVENT_MENU_STOP, 0);
                    simconnect.MenuAddSubItem(EVENT_ID.EVENT_MENU, "&Options", EVENT_ID.EVENT_MENU_OPTIONS, 0);
                    simconnect.MenuAddSubItem(EVENT_ID.EVENT_MENU, "&Clear User Aircraft Path", EVENT_ID.EVENT_MENU_CLEAR_USER_PATH, 0);
                }
                catch (COMException e)
                {
                    frmMain.NotifyError("FSX Add MenuItem failed!\n\n" + e.Message);
                }
            }
        }
        void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
/*            lock (lockUserAircraftID)
            {
                bUserAircraftIDSet = false;
            }

            if (gconffixCurrent.bQueryUserAircraft)
                timerQueryUserAircraft.Start();

            if (gconffixCurrent.bQueryUserPath)
                timerQueryUserPath.Start();

            if (gconffixCurrent.bUserPathPrediction)
                timerUserPrediction.Start();

            if (gconffixCurrent.bQueryAIObjects)
            {
                if (gconffixCurrent.bQueryAIAircrafts)
                    objAIAircrafts.timer.Start();

                if (gconffixCurrent.bQueryAIHelicopters)
                    objAIHelicopters.timer.Start();

                if (gconffixCurrent.bQueryAIBoats)
                    objAIBoats.timer.Start();

                if (gconffixCurrent.bQueryAIGroundUnits)
                    objAIGroundUnits.timer.Start();
            }

            timerIPAddressRefresh_Tick(null, null);
            timerIPAddressRefresh.Start();
 */ 
        }
        void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            if (timerConnect != null)
                timerConnect.Start();
            else
                frmMain.Close();
        }
        void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            frmMain.NotifyError( "FSX Exception!" );
        }
        void simconnect_OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            switch ((EVENT_ID)data.uEventID)
            {
                case EVENT_ID.EVENT_MENU_OPTIONS:
                    frmMain.Show();
                    break;
            }
        }
        void simconnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            StructBasicMovingSceneryObject obj = (StructBasicMovingSceneryObject)data.dwData[0];

            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_USER_AIRCRAFT:
                    lock (lockUserAircraft)
                    {
                        if (objUserAircraft != null)
                        {
                            objUserAircraft.Update(ref obj);
                            uiUserAircraftID = data.dwObjectID;
                        }
                        else
                            objUserAircraft = new SceneryMovingObject(data.dwObjectID, DATA_REQUESTS.REQUEST_USER_AIRCRAFT, ref obj);
                    }
                    break;
                case DATA_REQUESTS.REQUEST_AI_PLANE:
                    lock (objAIAircrafts.lockObject)
                    {
                        if (data.dwObjectID != uiUserAircraftID)
                        {
                            if (data.dwoutof == 0)
                                MarkDeletedObjects(ref objAIAircrafts.htObjects, obj.dTime);
                            else
                            {
                                if (objAIAircrafts.htObjects.ContainsKey(data.dwObjectID))
                                    ((SceneryMovingObject)objAIAircrafts.htObjects[data.dwObjectID]).Update(ref obj);
                                else
                                    objAIAircrafts.htObjects.Add(data.dwObjectID, new SceneryMovingObject(data.dwObjectID, DATA_REQUESTS.REQUEST_AI_PLANE, ref obj));
                                if (data.dwentrynumber == data.dwoutof && objAIAircrafts.htObjects.Count > data.dwoutof)
                                    MarkDeletedObjects(ref objAIAircrafts.htObjects, obj.dTime);
                            }
                        }
                    }
                    break;
                case DATA_REQUESTS.REQUEST_AI_HELICOPTER:
                    lock (objAIHelicopters.lockObject)
                    {
                        if (data.dwObjectID != uiUserAircraftID)
                        {
                            if (data.dwoutof == 0)
                                MarkDeletedObjects(ref objAIHelicopters.htObjects, obj.dTime);
                            else
                            {
                                if (objAIHelicopters.htObjects.ContainsKey(data.dwObjectID))
                                    ((SceneryMovingObject)objAIHelicopters.htObjects[data.dwObjectID]).Update(ref obj);
                                else
                                    objAIHelicopters.htObjects.Add(data.dwObjectID, new SceneryMovingObject(data.dwObjectID, DATA_REQUESTS.REQUEST_AI_HELICOPTER, ref obj));
                                if (data.dwentrynumber == data.dwoutof && objAIHelicopters.htObjects.Count > data.dwoutof)
                                    MarkDeletedObjects(ref objAIHelicopters.htObjects, obj.dTime);
                            }
                        }
                    }
                    break;
                case DATA_REQUESTS.REQUEST_AI_BOAT:
                    lock (objAIBoats.lockObject)
                    {
                        if (data.dwoutof == 0)
                            MarkDeletedObjects(ref objAIBoats.htObjects, obj.dTime);
                        else
                        {
                            if (objAIBoats.htObjects.ContainsKey(data.dwObjectID))
                                ((SceneryMovingObject)objAIBoats.htObjects[data.dwObjectID]).Update(ref obj);
                            else
                                objAIBoats.htObjects.Add(data.dwObjectID, new SceneryMovingObject(data.dwObjectID, DATA_REQUESTS.REQUEST_AI_BOAT, ref obj));
                            if (data.dwentrynumber == data.dwoutof && objAIBoats.htObjects.Count > data.dwoutof)
                                MarkDeletedObjects(ref objAIBoats.htObjects, obj.dTime);
                        }
                    }
                    break;
                case DATA_REQUESTS.REQUEST_AI_GROUND:
                    lock (objAIGroundUnits.lockObject)
                    {
                        if (data.dwoutof == 0)
                            MarkDeletedObjects(ref objAIGroundUnits.htObjects, obj.dTime);
                        else
                        {
                            if (objAIGroundUnits.htObjects.ContainsKey(data.dwObjectID))
                                ((SceneryMovingObject)objAIGroundUnits.htObjects[data.dwObjectID]).Update(ref obj);
                            else
                                objAIGroundUnits.htObjects.Add(data.dwObjectID, new SceneryMovingObject(data.dwObjectID, DATA_REQUESTS.REQUEST_AI_GROUND, ref obj));
                            if (data.dwentrynumber == data.dwoutof && objAIGroundUnits.htObjects.Count > data.dwoutof)
                                MarkDeletedObjects(ref objAIGroundUnits.htObjects, obj.dTime);
                        }
                    }
                    break;
                default:
#if DEBUG
                    frmMain.NotifyError("Received unknown data from FSX!");
#endif
                    break;
            }
        }

        protected void MarkDeletedObjects(ref Hashtable ht, double dTime)
        {
            foreach (DictionaryEntry entry in ht)
            {
                if (dTime == 0.0 || ((SceneryMovingObject)entry.Value).Time < dTime)
                {
                    ((SceneryMovingObject)entry.Value).State = SceneryMovingObject.STATE.DELETED;
                }
            }
        }
        
        public void CleanupHashtable(ref Hashtable ht)
        {
            ArrayList toDel = new ArrayList();
            foreach (DictionaryEntry entry in ht)
            {
                if (((SceneryMovingObject)entry.Value).State == SceneryObject.STATE.DELETED )
                {
                    toDel.Add(entry.Key);
                }
            }
            foreach (object key in toDel)
            {
                ht.Remove(key);
            }
        }

        public bool OnMessageReceive(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == WM_USER_SIMCONNECT)
            {
                if (simconnect != null)
                {
                    try
                    {
                        simconnect.ReceiveMessage();
                    }
                    catch(Exception e)
                    {
#if DEBUG
                        frmMain.NotifyError("Error receiving data from FSX!\n\n" + e.Message );
#endif
                    }
                }
                return true;
            }
  
            return false;
        }
        #endregion

        #region Timerfunctions
        private void OnTimerConnectElapsed(object sender, ElapsedEventArgs e)
        {
            if (openConnection())
            {
                timerConnect.Stop();
            }
        }
        private void OnTimerQueryUserAircraftElapsed(object sender, ElapsedEventArgs e)
        {
            lock (lockSimConnect)
            {
                try
                {
                    simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_USER_AIRCRAFT, DEFINITIONS.StructBasicMovingSceneryObject, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                }
                catch
                {
                }
            }
        }
        private void OnTimerQueryAIAircraftsElapsed(object sender, ElapsedEventArgs e)
        {
            lock (lockSimConnect)
            {
                try
                {
                    simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_AI_PLANE, DEFINITIONS.StructBasicMovingSceneryObject, (uint)Program.Config[Config.SETTING.QUERY_AI_AIRCRAFTS]["Range"].IntValue, SIMCONNECT_SIMOBJECT_TYPE.AIRCRAFT);
                
                }
                catch (COMException ex)
                {
                    frmMain.NotifyError(ex.Message);
                }
            }
        }
        private void OnTimerQueryAIHelicoptersElapsed(object sender, ElapsedEventArgs e)
        {
            lock (lockSimConnect)
            {
                try
                {
                    simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_AI_HELICOPTER, DEFINITIONS.StructBasicMovingSceneryObject, (uint)Program.Config[Config.SETTING.QUERY_AI_HELICOPTERS]["Range"].IntValue, SIMCONNECT_SIMOBJECT_TYPE.HELICOPTER);
                }
                catch
                {
                }
            }
        }
        private void OntimerQueryAIBoatsElapsed(object sender, ElapsedEventArgs e)
        {
            lock (lockSimConnect)
            {
                try
                {
                    simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_AI_BOAT, DEFINITIONS.StructBasicMovingSceneryObject, (uint)Program.Config[Config.SETTING.QUERY_AI_BOATS]["Range"].IntValue, SIMCONNECT_SIMOBJECT_TYPE.BOAT);
                }
                catch
                {
                }
            }
        }
        private void OnTimerQueryAIGroundUnitsElapsed(object sender, ElapsedEventArgs e)
        {
            lock (lockSimConnect)
            {
                try
                {
                    simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_AI_GROUND, DEFINITIONS.StructBasicMovingSceneryObject, (uint)Program.Config[Config.SETTING.QUERY_AI_GROUND_UNITS]["Range"].IntValue, SIMCONNECT_SIMOBJECT_TYPE.GROUND);
                }
                catch
                {
                }
            }
        }
        #endregion
    }
}
