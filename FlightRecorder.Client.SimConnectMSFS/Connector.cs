﻿using Microsoft.Extensions.Logging;
using Microsoft.FlightSimulator.SimConnect;
using System;

namespace FlightRecorder.Client.SimConnectMSFS
{
    public class Connector
    {
        private SimConnect simconnect = null;

        public event EventHandler<AircraftPositionUpdatedEventArgs> AircraftPositionUpdated;
        public event EventHandler Initialized;
        public event EventHandler<ConnectorErrorEventArgs> Error;
        public event EventHandler Closed;

        private readonly ILogger<Connector> logger;

        public Connector(ILogger<Connector> logger)
        {
            this.logger = logger;
        }

        public void Initialize(IntPtr Handle)
        {
            simconnect = new SimConnect("Flight Recorder", Handle, WM_USER_SIMCONNECT, null, 0);

            // listen to connect and quit msgs
            simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(Simconnect_OnRecvOpen);
            simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(Simconnect_OnRecvQuit);

            // listen to exceptions
            simconnect.OnRecvException += Simconnect_OnRecvException;
            simconnect.OnRecvEvent += Simconnect_OnRecvEvent;

            simconnect.OnRecvSimobjectData += Simconnect_OnRecvSimobjectData;
            RegisterAircraftPositionDefinition();

            //simconnect.SubscribeToSystemEvent(EVENTS.POSITION_CHANGED, "PositionChanged");

            simconnect.OnRecvSystemState += Simconnect_OnRecvSystemState;

            simconnect.MapClientEventToSimEvent(EVENTS.PAUSE, "PAUSE_ON");
            simconnect.MapClientEventToSimEvent(EVENTS.UNPAUSE, "PAUSE_OFF");
            simconnect.MapClientEventToSimEvent(EVENTS.LEFT_BRAKE_SET, "AXIS_LEFT_BRAKE_SET");
            simconnect.MapClientEventToSimEvent(EVENTS.RIGHT_BRAKE_SET, "AXIS_RIGHT_BRAKE_SET");

            Initialized?.Invoke(this, new());
        }

        public void Pause()
        {
            //simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.PAUSE, 0, GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
        }

        public void Unpause()
        {
            //simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.UNPAUSE, 0, GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
        }

        public void LeftBrake(double amount)
        {
            //simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.LEFT_BRAKE_SET, (uint)(amount * 16384), GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
        }

        public void RightBrake(double amount)
        {
            //simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.RIGHT_BRAKE_SET, (uint)(amount * 16384), GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
        }

        public void Set(AircraftPositionStruct position)
        {
            simconnect.SetDataOnSimObject(DEFINITIONS.AircraftPosition, 0, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
        }

        private void RegisterAircraftPositionDefinition()
        {
            RegisterDataDefinition<AircraftPositionStruct>(DEFINITIONS.AircraftPosition,
                //("SIMULATION RATE", "number", SIMCONNECT_DATATYPE.INT32),

                ("PLANE LATITUDE", "Degrees", SIMCONNECT_DATATYPE.FLOAT64),
                ("PLANE LONGITUDE", "Degrees", SIMCONNECT_DATATYPE.FLOAT64),
                ("PLANE ALTITUDE", "Feet", SIMCONNECT_DATATYPE.FLOAT64),

                ("PLANE PITCH DEGREES", "Degrees", SIMCONNECT_DATATYPE.FLOAT64),
                ("PLANE BANK DEGREES", "Degrees", SIMCONNECT_DATATYPE.FLOAT64),
                ("PLANE HEADING DEGREES TRUE", "Degrees", SIMCONNECT_DATATYPE.FLOAT64),
                ("PLANE HEADING DEGREES MAGNETIC", "Degrees", SIMCONNECT_DATATYPE.FLOAT64),

                ("VELOCITY BODY X", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64),
                ("VELOCITY BODY Y", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64),
                ("VELOCITY BODY Z", "Feet per second", SIMCONNECT_DATATYPE.FLOAT64),
                ("ROTATION VELOCITY BODY X", "radians per second", SIMCONNECT_DATATYPE.FLOAT64),
                ("ROTATION VELOCITY BODY Y", "radians per second", SIMCONNECT_DATATYPE.FLOAT64),
                ("ROTATION VELOCITY BODY Z", "radians per second", SIMCONNECT_DATATYPE.FLOAT64),

                ("AILERON POSITION", "Position", SIMCONNECT_DATATYPE.FLOAT64),
                ("ELEVATOR POSITION", "Position", SIMCONNECT_DATATYPE.FLOAT64),
                ("RUDDER POSITION", "Position", SIMCONNECT_DATATYPE.FLOAT64),

                ("ELEVATOR TRIM POSITION", "Radians", SIMCONNECT_DATATYPE.FLOAT64),

                ("TRAILING EDGE FLAPS LEFT PERCENT", "Position", SIMCONNECT_DATATYPE.FLOAT64),
                ("TRAILING EDGE FLAPS RIGHT PERCENT", "Position", SIMCONNECT_DATATYPE.FLOAT64),
                ("LEADING EDGE FLAPS LEFT PERCENT", "Position", SIMCONNECT_DATATYPE.FLOAT64),
                ("LEADING EDGE FLAPS RIGHT PERCENT", "Position", SIMCONNECT_DATATYPE.FLOAT64),

                ("GENERAL ENG THROTTLE LEVER POSITION:1", "Position", SIMCONNECT_DATATYPE.FLOAT64),
                ("GENERAL ENG THROTTLE LEVER POSITION:2", "Position", SIMCONNECT_DATATYPE.FLOAT64),
                ("GENERAL ENG THROTTLE LEVER POSITION:3", "Position", SIMCONNECT_DATATYPE.FLOAT64),
                ("GENERAL ENG THROTTLE LEVER POSITION:4", "Position", SIMCONNECT_DATATYPE.FLOAT64),

                ("BRAKE LEFT POSITION", "Position", SIMCONNECT_DATATYPE.FLOAT64),
                ("BRAKE RIGHT POSITION", "Position", SIMCONNECT_DATATYPE.FLOAT64)
            );
        }

        private void ProcessAircraftPosition(AircraftPositionStruct position)
        {
            logger.LogTrace("Get Aircraft status");
            AircraftPositionUpdated?.Invoke(this, new AircraftPositionUpdatedEventArgs(position));
        }

        private void RequestDataOnConnected()
        {
            simconnect.RequestDataOnSimObject(
                DATA_REQUESTS.AIRCRAFT_POSITION, DEFINITIONS.AircraftPosition, 0,
                SIMCONNECT_PERIOD.SIM_FRAME,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0);
        }

        #region Facility

        // User-defined win32 event
        const int WM_USER_SIMCONNECT = 0x0402;

        // Simconnect client will send a win32 message when there is
        // a packet to process. ReceiveMessage must be called to
        // trigger the events. This model keeps simconnect processing on the main thread.
        public IntPtr HandleSimConnectEvents(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam, ref bool isHandled)
        {
            isHandled = false;

            switch (message)
            {
                case WM_USER_SIMCONNECT:
                    {
                        if (simconnect != null)
                        {
                            try
                            {
                                this.simconnect.ReceiveMessage();
                            }
                            catch (Exception ex)
                            {
                                RecoverFromError(ex);
                            }

                            isHandled = true;
                        }
                    }
                    break;

                default:
                    logger.LogTrace("Unknown message type: {message}", message);
                    break;
            }

            return IntPtr.Zero;
        }

        private void RecoverFromError(Exception exception)
        {
            // 0xC000014B: CTD
            // 0xC00000B0: Sim has exited
            logger.LogError(exception, "Cannot receive SimConnect message!");
            //CloseConnection();
            Closed?.Invoke(this, new());
        }

        void Simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            logger.LogInformation("Connected to Flight Simulator {applicationName}", data.szApplicationName);
            RequestDataOnConnected();
        }

        void Simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            logger.LogInformation("Flight Simulator has exited");
            Closed?.Invoke(this, new());
            //CloseConnection();
        }

        void Simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            var error = (SIMCONNECT_EXCEPTION)data.dwException;
            logger.LogError("SimConnect error received: {error}", error);

            Error?.Invoke(this, new ConnectorErrorEventArgs(error));
        }


        private void Simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            // Must be general SimObject information
            switch (data.dwRequestID)
            {
                case (uint)DATA_REQUESTS.AIRCRAFT_POSITION:
                    {
                        var position = data.dwData[0] as AircraftPositionStruct?;
                        if (position.HasValue)
                        {
                            ProcessAircraftPosition(position.Value);
                        }
                    }
                    break;
            }
        }

        void Simconnect_OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            logger.LogDebug("OnRecvEvent dwID {dwID} uEventID {uEventID}", (SIMCONNECT_RECV_ID)data.dwID, data.uEventID);
        }

        private void Simconnect_OnRecvSystemState(SimConnect sender, SIMCONNECT_RECV_SYSTEM_STATE data)
        {
            logger.LogDebug("OnRecvSystemState dwRequestID {dwRequestID}", (DATA_REQUESTS)data.dwRequestID);
        }

        private void RegisterDataDefinition<T>(DEFINITIONS definition, params (string datumName, string unitsName, SIMCONNECT_DATATYPE datumType)[] data)
        {
            foreach (var (datumName, unitsName, datumType) in data)
            {
                simconnect.AddToDataDefinition(definition, datumName, unitsName, datumType, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            }
            simconnect.RegisterDataDefineStruct<T>(definition);
        }

        #endregion
    }
}