using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst.CompilerServices;
using Unity.Mathematics;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using Unity.VisualScripting.FullSerializer;
using UnityEditor.SceneManagement;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;
using static Game;
using static GUI;
using static Piece;
using static Unity.IntegerTime.RationalTime;
using static Unity.VisualScripting.Member;
using static UnityEditor.PlayerSettings;
using static UnityEditor.ShaderKeywordFilter.FilterAttribute;

public class GUI : MonoBehaviour
{
    public static Storage storage;
    public static Game game;

    public Game _gameRef;
    public Storage _storageRef;

    //
    ////
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static class Settings
    {
        public static class Draw {
            public static bool drawWhiteControlledSquares = true;
            public static bool drawWhiteSelectedPieceLegalSquares = true;

            public static bool drawBlackControlledSquares = true;
            public static bool drawBlackSelectedPieceLegalSquares = true;
        }

        public static class Debug {
            public static bool DEBUGMODE = false;
            public static KeyCode debugKey = KeyCode.None;


            public static bool pulseHistory = false;

			public static (Pulse pulse, string trace)? debugInfoPulse = null;
			public static int debugInfoPulseSelect = 0;
		}

        public static class Pulsing {
            public static int fadeInXFrames = 5;
            public static float slowMotionCoefficient = 1.5f;

		}
        
    }
    ////
    //
    ////GUI Stuff
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // Debug-String stuff, disable printDebugString in settings if to not be used
    HashSet<(HashSet<Vector2> pinLine, Piece pinningPiece, Piece pinnedPiece, Piece pieceAtPinEnd)> pins = new HashSet<(HashSet<Vector2> pinLine, Piece pinningPiece, Piece pinnedPiece, Piece pieceAtPinEnd)>();
    //

    public static class DebugInfo
    {
        public static int pieceCalculationsRunning = 0;
        public static int pulsesRunning = 0;
        public static int pulsesWaiting = 0;
    }



    static Piece pieceSelected;

    public static Dictionary<int, string[]> moveNotations = new Dictionary<int, string[]>();
    public static Dictionary<int, Dictionary<int, List<Vector2>>> changedPiecesMoves = new Dictionary<int, Dictionary<int, List<Vector2>>>(48);

    public static Dictionary<int, HashSet<Vector2>> controlledSquaresCache = new Dictionary<int, HashSet<Vector2>>();

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////
    //


    //


    void Start()
    {
        game = _gameRef;
        storage = _storageRef;
    }


    float frameCounter = 0;
    void Update()
    {

        if (Input.GetMouseButtonDown(0) && !game.gameOver)
        {
            Vector2 pos = storage.camera.ScreenToWorldPoint(Input.mousePosition);

            for (int i = 0; i < 2; i++) { pos[i] = Mathf.Round(pos[i]); }

            if (pos.x <= 8 && pos.x >= 1 && pos.y <= 8 && pos.y >= 1) _MousePressedBoard(pos);
        }


        if (Input.GetKeyDown(KeyCode.W))
        {
            Settings.Debug.debugKey = KeyCode.W;
            GUIUPDATE();
        } else if (Input.GetKeyDown(KeyCode.B))
        {
            Settings.Debug.debugKey = KeyCode.B;
            GUIUPDATE();
        }
		else if (Input.GetKeyDown(KeyCode.A))
		{
			Settings.Debug.debugKey = KeyCode.A;
			GUIUPDATE();
		} else if (Input.GetKeyDown(KeyCode.R))
        {
            Settings.Debug.debugKey = KeyCode.None;
            storage.board.RefreshSquares();
        } else if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            Settings.Debug.DEBUGMODE = !Settings.Debug.DEBUGMODE;
            storage.DebugText.enabled = Settings.Debug.DEBUGMODE;
        } 
        
        else if (Input.GetKeyDown(KeyCode.Comma))
        {
            if (Settings.Debug.pulseHistory)
            {
				Settings.Debug.debugInfoPulseSelect = -1;
			}
        } 

        else if (Input.GetKeyDown(KeyCode.Period))
        {
			if (Settings.Debug.pulseHistory)
			{
				Settings.Debug.debugInfoPulseSelect = 1;
			}
		}

        else if (Input.GetKeyDown(KeyCode.Semicolon))
        {
            Settings.Debug.pulseHistory = !Settings.Debug.pulseHistory;
        }


        
        if (!Scheduler._PAUSECYCLE_)
        {
            frameCounter += Time.deltaTime;
            if (frameCounter >= (float) 1 / Scheduler.ticksPerSecond) { Scheduler.AwaitPulseCalcDoneThenCycle(); frameCounter -= (((float)1 / (float)Scheduler.ticksPerSecond) * Settings.Pulsing.slowMotionCoefficient); }
        }

    }

    public static Vector2? GetMousePosition()
    {

        Vector2 pos = storage.camera.ScreenToWorldPoint(Input.mousePosition);
        for (int i = 0; i < 2; i++) { pos[i] = Mathf.Round(pos[i]); }

        if (pos.x <= 8 && pos.x >= 1 && pos.y <= 8 && pos.y >= 1) { return pos; }
        else return null;
    }


	/// <param name="specificPosition">  | specific position to read pulse debug info from | -ByDefault: mouse position  </param> 
	/// <param name="specificPulse">  | specific pulse to read debug info from at position | -ByDefault: active pulse at position  </param>
	/// <summary>  set the pulse-debug-info text to the specified pulse at specified position  </summary> 
	public static void SetSquareDebugInfo(Vector2 specificPosition, Pulse specificPulse) { __SetSquareDebugText__(specificPosition, specificPulse); }

	/// <summary>  set the pulse-debug-info text to the active pulse at specified position  </summary>
	///<inheritdoc cref="SetSquareDebugInfo(Vector2, Pulse)"/>
	public static void SetSquareDebugInfo(Vector2 specificPosition) { __SetSquareDebugText__(specificPosition, null); }

	/// <summary>  set the pulse-debug-info text to the active pulse at specified position  </summary>
	///<inheritdoc cref="SetSquareDebugInfo(Vector2, Pulse)"/>
	public static void SetSquareDebugInfo(Pulse specificPulse) { __SetSquareDebugText__(null, specificPulse); }

	/// <summary>  set the pulse-debug-info text to the active pulse at mouse position  </summary>
	public static void SetSquareDebugInfo() { __SetSquareDebugText__(null, null); }



    static int? pulseTimelineSelectIndex = null;
    static Vector2? positionCache = null;
	static void __SetSquareDebugText__(Vector2? specificPosition, Pulse? specificPulse)
	{
		Vector2 pos;

		//if no position specified set position to mouse position
		if (specificPosition is not null) pos = (Vector2)specificPosition;

		else { 
            var mousePosition = GetMousePosition();
            if (mousePosition is null) return;

            else pos = (Vector2)mousePosition;   
        }



		int historyCount = Tracing.GetTraceHistoryCount(pos);


		if (Settings.Debug.pulseHistory == false) 
		{
			Settings.Debug.debugInfoPulseSelect = 0;

			storage.PulseHistoryControlText.text = $"PulseHistory:<b><Color=#ff9e9e>Disabled</b></Color> HistoryIndex:{Tracing.GetTraceHistoryCount(pos) - 1}/{Tracing.GetTraceHistoryCount(pos) - 1}";
			FindActiveSquareDebugInfo(null);

            return;
		}


        storage.PulseHistoryControlText.text = $"PulseHistory:<b><Color=#9effba>Enabled</b></Color> HistoryIndex:{pulseTimelineSelectIndex}/{historyCount - 1}";


		if (historyCount < 1) { Settings.Debug.debugInfoPulseSelect = 0; return; }

		if (positionCache != pos)
		{

			pulseTimelineSelectIndex = historyCount - 1;
			Settings.Debug.debugInfoPulse = Tracing.GetTraceHistory(pos, historyCount - 1);
			Settings.Debug.debugInfoPulseSelect = 0;

			positionCache = pos;

		}

		if (Settings.Debug.debugInfoPulseSelect != 0)
		{

			pulseTimelineSelectIndex += Settings.Debug.debugInfoPulseSelect;

			if (historyCount > 1)
			{
                //if index above or below historyIndex loop around
				if (pulseTimelineSelectIndex > historyCount - 1) pulseTimelineSelectIndex = (pulseTimelineSelectIndex - (historyCount));
				else if (pulseTimelineSelectIndex < 0) pulseTimelineSelectIndex = (historyCount) + pulseTimelineSelectIndex;
                //

                
				if (pulseTimelineSelectIndex < 0) { pulseTimelineSelectIndex = historyCount - 1; }

			}
			else { pulseTimelineSelectIndex = 0; }


			var history = ((Pulse pulse, string trace)) Tracing.GetTraceHistory(pos, (int) pulseTimelineSelectIndex);

			storage.PulseTraceText.text = history.trace;
            FindActiveSquareDebugInfo(history.pulse);

			Settings.Debug.debugInfoPulseSelect = 0;

		}


		
	}

    static void FindActiveSquareDebugInfo(Pulse? pulse)
    {

        Vector2? pos = pulse is null ? GetMousePosition() : pulse.config.position;

        if (pos is null) return;

        if (pulse is null)
        {
			if ( Scheduler.pulsesActiveTrack.TryGetValue((Vector2)pos, out var activePulse) ) pulse = activePulse;
		}



		String squareColor = null;
		String pieceOnSquare = "None";


		String pulseColor = null;
		String pulseCachColor = null;
		String pulseResetColor = null;
		String pulseStartingColor = null;

		bool? pulseEnded = null;

		int? pulseStartedOnTick = null;
		int pulsePrecedence = 0;
		int currentPulseTick = 0;
		int pulseTotalTickDuration = 0;

		PulseConfig.HoldMode? pulseHoldMode = null;
		PulseConfig.HoldEndCondition? pulseHoldEndCondition = null;




		squareColor = ColorToFormattedString((Color)storage.board.squares[(Vector2)pos].material.color);
		if (game.pieceBoard.TryGetValue((Vector2)pos, out var piece)) pieceOnSquare = $"{piece.colour}_{piece.type}_ID{piece.id}";

        
        if (pulse is not null)
        {
			PulseConfig cfg = pulse.config;


			pulseColor = ColorToFormattedString(cfg.pulseColour);
			pulseCachColor = ColorToFormattedString((Color)cfg.cachedColour);
			pulseResetColor = ColorToFormattedString((Color)cfg.resetColour);
			pulseStartingColor = ColorToFormattedString(pulse.startingColour);

			pulsePrecedence = pulse.config.precedence;
			pulseEnded = pulse._PULSEENDED_;

			pulseStartedOnTick = pulse.frameStarted;
			currentPulseTick = pulse.totalFramesRun;
			pulseTotalTickDuration = pulse.totalFrameDuration;

			pulseHoldMode = cfg.holdMode;
			if (pulseHoldMode == PulseConfig.HoldMode.hold) pulseHoldEndCondition = cfg.holdEndCondition;


			if (Settings.Debug.pulseHistory == false) storage.PulseTraceText.text = Tracing.Get(pulse);
		}

        var squareDebugText = $"<Square Details>\r\n Position:{pos} Color:{squareColor}\n Piece:{pieceOnSquare}\n\n<Square's Active Pulse Details>\n Color:{pulseColor}\n CachColor:{pulseCachColor}\n ResetColor:{pulseResetColor}\n Precedence:{pulsePrecedence} PulseEnded:{pulseEnded}\n StartingColor:{pulseStartingColor}\n\n StartedOnTick:{pulseStartedOnTick}   Tick:{currentPulseTick}/{pulseTotalTickDuration}\n HoldMode:{pulseHoldMode}  HoldEndCondition:{pulseHoldEndCondition}";

        if (pulse is null) squareDebugText += $"\n\n < Pulse Stats >\n TotalRunningPulses:{ DebugInfo.pulsesRunning }\n CurrentTick:{ Scheduler.totalTickTimer }";


		storage.DebugText.text = squareDebugText;
	}



	public class Cache
    {

        Dictionary<Vector2, Color> underPulseBoardColour = new Dictionary<Vector2, Color>(64); 

    }
    //
    


    //
    public class PulseGroup {
        public GroupConfig groupConfig;

        List<Pulse> pulseGroup;
        public HashSet< Vector2 > pulsePositions;

        public PulseGroup( List<Pulse> pulses, GroupConfig config ) {

            pulseGroup = pulses;
            groupConfig = config;
            pulsePositions = new HashSet<Vector2>(pulses.Count);
            foreach (var pulse in pulses) { pulsePositions.Add( pulse.config.position ); }

        }
        public PulseGroup( int size, GroupConfig config ) {

            pulseGroup = new List<Pulse>(size);
            groupConfig = config;
            pulsePositions = new HashSet<Vector2>(size);

        }
        public PulseGroup( int size ) {

            pulseGroup = new List<Pulse>(size);
            pulsePositions = new HashSet<Vector2>(size);

        }

        public void SetConfig( GroupConfig config ) {

            groupConfig = config;

        }


        public void Add( Pulse pulse ) {
            pulseGroup.Add(pulse);
            pulsePositions.Add(pulse.config.position);
        }
        public List<Pulse> GetPulses() {

            if (groupConfig is null) throw new Exception("GroupConfig was not set");

            //this don't diggidy dang work because you set the size beforehand- remember that if it comes up and maybe fix it too
            if (pulsePositions.Count != pulseGroup.Count) throw new Exception("Count of pulsePositions does not match count of pulses in pulseGroup");

            return pulseGroup;

        }
    }
    //



    //
    public class GroupConfig
    {
        public int precedence { get; private set; } = 0;
        public int framesBeforeStarting { get; private set; } = 0;
        public int framesBetweenElements { get; private set; } = 0;

        public GroupConfig( int _precedenceLevel, int _framesBetweenElements, int _framesBeforeStarting ) { precedence = _precedenceLevel; framesBeforeStarting = _framesBeforeStarting; framesBetweenElements = _framesBetweenElements; }
    }
    public class PulseGroupGroup {
        public GroupConfig groupConfig;

        List<PulseGroup> pulseGroupGroup;

        public PulseGroupGroup( int size, GroupConfig config ) {

            pulseGroupGroup = new List<PulseGroup>(size);
            groupConfig = config;
        }

        public void Add( PulseGroup pulseGroup ) {

            pulseGroupGroup.Add(pulseGroup);
        }

        public List<PulseGroup> GetPulseGroups() {

            if (groupConfig is null) throw new Exception("GroupConfig was null");
            return pulseGroupGroup;
        }
    }
    //



    //
    public class PulseConfig {
        public enum HoldMode { noHold, hold };
        public enum HoldEndCondition { whenInterruptedBySamePrecedence, whenNextTurn, whenInterruptedByAnyPrecedence };


        public int precedence = 0;

        public Color? resetColour;
        public Color? cachedColour;

        public Vector2 position { get; private set; }
        public Color pulseColour;
        public int holdColourForXFramesInIteration { get; private set; }
        public int lerpInXFramesInIteration { get; private set; }
        public int runXIterations { get; private set; }

        public HoldMode holdMode { get; private set; } = HoldMode.noHold;
        public HoldEndCondition holdEndCondition { get; private set; } = HoldEndCondition.whenNextTurn;

        public PulseConfig( Vector2 _position, Color _pulseColour, Color _resetColour, int _precedenceLevel, int _holdColourForXFramesInIteration, int _lerpInXFramesInIteration, int _runXIterations ) { position = _position; precedence = _precedenceLevel; pulseColour = _pulseColour; resetColour = _resetColour; holdColourForXFramesInIteration = _holdColourForXFramesInIteration; lerpInXFramesInIteration = _lerpInXFramesInIteration; runXIterations = _runXIterations; }
        public PulseConfig( Vector2 _position, Color _pulseColour,                     int _precedenceLevel, int _holdColourForXFramesInIteration, int _lerpInXFramesInIteration, int _runXIterations ) { position = _position; pulseColour = _pulseColour; precedence = _precedenceLevel; holdColourForXFramesInIteration = _holdColourForXFramesInIteration; lerpInXFramesInIteration = _lerpInXFramesInIteration; runXIterations = _runXIterations; }
        public PulseConfig( Vector2 _position, Color _pulseColour, Color _resetColour, int _precedenceLevel, int _holdColourForXFramesInIteration, int _lerpInXFramesInIteration, int _runXIterations, HoldMode _holdMode, HoldEndCondition _holdEndCondition ) { position = _position; precedence = _precedenceLevel; pulseColour = _pulseColour; resetColour = _resetColour; holdColourForXFramesInIteration = _holdColourForXFramesInIteration; lerpInXFramesInIteration = _lerpInXFramesInIteration; runXIterations = _runXIterations; holdMode = _holdMode; holdEndCondition = _holdEndCondition; }

    }
    //
    //
    public class Pulse {
        public PulseConfig config { get; private set; }


        public Material square { get; private set; }
        public Color startingColour;


        public int totalFrameDuration { get; private set; }

        public int frameStarted;

        public int totalFramesRun = 0;
        public int iteration = 1;
        public int fadeFramesRunInIteration = 0;
        public int holdFramesRunInIteration = 0;
        public int lerpFramesRunInIteration = 0;




        public bool _PULSEENDED_ = false;

        public Pulse( PulseConfig _config )
        {
            config = _config;
            totalFrameDuration = ( GUI.Settings.Pulsing.fadeInXFrames + config.lerpInXFramesInIteration + config.holdColourForXFramesInIteration + (config.runXIterations-1) ) * config.runXIterations;
            square = storage.board.squares[config.position].material;
            startingColour = square.color;
        }

        public void _FastForwardToFrame_(int frame)
        {
			Tracing.Add(this, Tracing.TraceType.Action, $"FastFrwd_F:{frame}");
			if (frame >= totalFrameDuration || Scheduler.holdingPulses.Contains(this))
            {
                if (config.holdMode == PulseConfig.HoldMode.hold) { square.color = (Color)config.resetColour; totalFramesRun = totalFrameDuration; }

                return;
            }



            int frameCalc = frame;

            while (true)
            {


                

                frameCalc -= Settings.Pulsing.fadeInXFrames;
                if (frameCalc >= 0)
                {
                    fadeFramesRunInIteration = Settings.Pulsing.fadeInXFrames;
                    totalFramesRun += Settings.Pulsing.fadeInXFrames;

                    square.color = config.pulseColour;
                }
                else
                {
                    Color staticSquareColor = square.color;

                    fadeFramesRunInIteration = Settings.Pulsing.fadeInXFrames + frameCalc;
                    totalFramesRun += Settings.Pulsing.fadeInXFrames + frameCalc;

                    square.color = Color.Lerp(staticSquareColor, config.pulseColour, ((float)fadeFramesRunInIteration / (float)Settings.Pulsing.fadeInXFrames));
                    return;
                }



                frameCalc -= config.holdColourForXFramesInIteration;

                if (frameCalc >= 0)
                {
                    holdFramesRunInIteration = config.holdColourForXFramesInIteration;
                    totalFramesRun += config.holdColourForXFramesInIteration;
                }
                else
                {
                    holdFramesRunInIteration = config.holdColourForXFramesInIteration + frameCalc;
                    totalFramesRun += config.holdColourForXFramesInIteration + frameCalc;
                    return;
                }



                frameCalc -= config.lerpInXFramesInIteration;

                if (frameCalc >= 0)
                {
                    square.color = config.resetColour is null ? (Color)config.cachedColour : (Color)config.resetColour;

                    lerpFramesRunInIteration = config.lerpInXFramesInIteration;
                    totalFramesRun += config.lerpInXFramesInIteration;
                }
                else
                {
					config.resetColour = config.resetColour is null ? (Color)config.cachedColour : (Color)config.resetColour;

					square.color = Color.Lerp(square.color, (Color)config.resetColour, ((float)(config.lerpInXFramesInIteration + frameCalc) / (float)config.lerpInXFramesInIteration));
                    lerpFramesRunInIteration = config.lerpInXFramesInIteration + frameCalc;
                    totalFramesRun += config.lerpInXFramesInIteration + frameCalc;
                    return;
                }

                if (frameCalc <= 0) return;

                totalFramesRun += 1;

                fadeFramesRunInIteration = 0;
                holdFramesRunInIteration = 0;
                lerpFramesRunInIteration = 0;

                iteration += 1;
            }
        }
    }
    //





    //
    public static class Scheduler
    {
        public static float ticksPerSecond = 120;

        //


        public static bool _PAUSECYCLE_ { get; private set; } = false;

        //


        public static int totalTickTimer { get; private set; } = 0;
        public static bool clock { get; private set; } = false;



        static int pulseCalculationsRunning = 0;

        //Dictionary< startTick, Dictionary< precedence, List<PulseGroupGroup> > >
        static Dictionary<int, Dictionary<int, List<PulseGroupGroup>>> pulseGroupGroupsScheduledTrack = new Dictionary<int, Dictionary<int, List<PulseGroupGroup>>>(4);

        //Dictionary< startTick, Dictionary< precedence, List<PulseGroup> > >
        static Dictionary<int, Dictionary<int, List<PulseGroup>>> pulseGroupsScheduledTrack = new Dictionary<int, Dictionary<int, List<PulseGroup>>>(16);

        //Dictionary< startTick, Dictionary< precedence, List<Pulse> > >
        static Dictionary<int, Dictionary<int, List<Pulse>>> pulsesScheduledTrack = new Dictionary<int, Dictionary<int, List<Pulse>>>(64);


        //Dictionary< position, Dictionary< precedence, List<Pulse> > >
        public static Dictionary<Vector2, Dictionary<int, List<Pulse>>> pulsesRunningTrack = new Dictionary<Vector2, Dictionary<int, List<Pulse>>>(64);
        public static Dictionary<Vector2, Pulse> pulsesActiveTrack = new Dictionary<Vector2, Pulse>(64);

        public static HashSet<Pulse> holdingPulses = new HashSet<Pulse>();

        


        public static void AwaitPulseCalcDoneThenCycle()
        {
            _Cycle();
        }

        static void _Cycle()
        {
            _PAUSECYCLE_ = true;
            clock = !clock;
            totalTickTimer++;

            _FrameUpdate();
        }

        static void _FrameUpdate()
        {

            bool tickHasScheduledPulseGG = pulseGroupGroupsScheduledTrack.TryGetValue(totalTickTimer, out var pGGPrecedenceDict);

            if (tickHasScheduledPulseGG)
            {

                for (int precedence = 0; precedence < 10; precedence++)
                {
                    if (!pGGPrecedenceDict.ContainsKey(precedence)) continue;


                    List<PulseGroupGroup> pulseGGList = new List<PulseGroupGroup>(pGGPrecedenceDict[precedence]);

                    foreach (var pulseGG in pulseGGList)
                    {
                        GroupConfig pulseGGConfig = pulseGG.groupConfig;


                        int tickTimerCalc = pulseGGConfig.framesBeforeStarting;

                        foreach (var pulseG in pulseGG.GetPulseGroups()) {

                            GroupConfig pulseGConfig = pulseG.groupConfig;
                            SchedulePulseGroup(pulseG, tickTimerCalc);

                            tickTimerCalc += pulseGGConfig.framesBetweenElements;
                        }


                        pulseGroupGroupsScheduledTrack[totalTickTimer][precedence].Remove(pulseGG);
                    }
                }
            }




            bool tickHasScheduledPulseG = pulseGroupsScheduledTrack.TryGetValue(totalTickTimer, out var pGPrecedenceDict);

            if (tickHasScheduledPulseG)
            {
                for (int precedence = 0; precedence < 10; precedence++)
                {
                    if (!pGPrecedenceDict.ContainsKey(precedence)) continue;


                    List<PulseGroup> pulseGList = new List<PulseGroup>(pGPrecedenceDict[precedence]);

                    foreach (var pulseG in pulseGList)
                    {
                        GroupConfig pulseGConfig = pulseG.groupConfig;


                        int tickTimerCalc = pulseGConfig.framesBeforeStarting;

                        foreach (var pulse in pulseG.GetPulses()) {

                            PulseConfig pulseConfig = pulse.config;

							SchedulePulse(pulse, tickTimerCalc);

                            tickTimerCalc += pulseGConfig.framesBetweenElements;
                        }


                        pulseGroupsScheduledTrack[totalTickTimer][precedence].Remove(pulseG);
                    }
                }
            }



            bool tickHasScheduledPulse = pulsesScheduledTrack.TryGetValue(totalTickTimer, out var pPrecedenceDict);

            if (tickHasScheduledPulse)
            {
                for (int precedence = 0; precedence < 10; precedence++)
                {
                    if (!pPrecedenceDict.ContainsKey(precedence)) continue;


                    List<Pulse> pulseList = new List<Pulse>(pPrecedenceDict[precedence]);


                    foreach (var pulse in pulseList) {


                        bool pulseRunningOnSquare = pulsesActiveTrack.TryGetValue(pulse.config.position, out var activePulse);
						_AddToRunningPulses(pulse);
						if (pulseRunningOnSquare && activePulse.Equals(pulse) || holdingPulses.Contains(pulse) || pulse._PULSEENDED_) continue;


						pulse.frameStarted = totalTickTimer;


						bool isHigherPrecedenceThanRunning = pulseRunningOnSquare ? pulse.config.precedence < activePulse.config.precedence : false;
                        bool isSamePrecedenceAsRunning = pulseRunningOnSquare ? pulse.config.precedence == activePulse.config.precedence : false;


						if (isHigherPrecedenceThanRunning)
						{
							if (activePulse.config.holdMode == PulseConfig.HoldMode.hold && activePulse.config.holdEndCondition != PulseConfig.HoldEndCondition.whenInterruptedByAnyPrecedence)
							{
								Tracing.Add(pulse, Tracing.TraceType.Set, $"cachClrAndRstClr=HeldPulse");

								pulse.config.resetColour = activePulse.square.color;
								pulse.config.cachedColour = activePulse.config.cachedColour;

                                _RemoveFromActivePulses(activePulse);
								//SchedulePulse(activePulse, pulse.totalFrameDuration);
							}
							else
							{
								Tracing.Add(pulse, Tracing.TraceType.Set, $"cachColr=ActivePulse");
								pulse.config.cachedColour = activePulse.config.cachedColour;
                                pulse.config.resetColour = activePulse.square.color;

								_RemoveFromActivePulses(activePulse);
							}
						}


						if ( pulseRunningOnSquare ) 
                        {
                            if ( (activePulse.config.holdEndCondition == PulseConfig.HoldEndCondition.whenInterruptedBySamePrecedence && isSamePrecedenceAsRunning) || activePulse.config.holdEndCondition == PulseConfig.HoldEndCondition.whenInterruptedByAnyPrecedence ) 
                            {
								Tracing.Add(activePulse, Tracing.TraceType.Set, $"ended=1");
                                activePulse._PULSEENDED_ = true;


								Tracing.Add(pulse, Tracing.TraceType.Set, $"cachColr=EndedSamePrec");
								pulse.config.cachedColour = activePulse.config.cachedColour;
                            }
                        }


                        if ( isHigherPrecedenceThanRunning || (!pulseRunningOnSquare) )
                        {
                            if (pulseRunningOnSquare)
                            {
                                pulse.config.cachedColour = activePulse.config.cachedColour;


                                int activePulseFramesWhenPulseEnds = (pulse.frameStarted - activePulse.frameStarted) + pulse.totalFrameDuration;

                                if (( activePulseFramesWhenPulseEnds < activePulse.totalFrameDuration || holdingPulses.Contains(activePulse) ) && !activePulse._PULSEENDED_) {

                                    activePulse._FastForwardToFrame_(activePulseFramesWhenPulseEnds);
                                    pulse.config.resetColour = activePulse.square.color;

                                    if (holdingPulses.Contains(activePulse)) pulse.config.cachedColour = activePulse.square.color;
                                }
                                else {
                                    pulse.config.resetColour = activePulse.config.cachedColour;
								}
                            }

							Tracing.Add(pulse, Tracing.TraceType.Action, $"initPulse");
							PulseHandler.New(pulse);

							_AddToActivePulses(pulse);
                        }


                    }

                }
                pulsesScheduledTrack[totalTickTimer].Clear();
            }

			SetSquareDebugInfo();

			_ActOnActivePulses();


            //if (totalTickTimer % 10 == 0) _ClearExpiredPulses();



            _PAUSECYCLE_ = false;
        }



        static void _ActOnActivePulses()
        {
            Dictionary<Vector2, Pulse> activePulses = new Dictionary<Vector2, Pulse>(pulsesActiveTrack);

            foreach (var activePulse in activePulses.Values)
            {
                if (activePulse._PULSEENDED_ == false) PulseHandler.HandlePulseAction(activePulse);


                if ( activePulse._PULSEENDED_ == true)
                {
                    Debug.Log("ended");
                    _RemoveFromActivePulses(activePulse);

                    _RemoveFromRunningPulses(activePulse);

					if (holdingPulses.Contains(activePulse)) _RemoveFromHoldingPulses(activePulse);


					_FindHighestPrecedenceAndReplace(activePulse);
					SetSquareDebugInfo();
				} 
            }
        }

        static void _FindHighestPrecedenceAndReplace(Pulse replacePulse)
        {
			//if no other pulses on square set square color to cached color
			if (!pulsesRunningTrack.TryGetValue(replacePulse.config.position, out var runningPrecedenceDict)) {
                replacePulse.square.color = (Color)replacePulse.config.cachedColour;
                return;
            }

			replacePulse.square.color = (Color)replacePulse.config.cachedColour;


			for (int precedence = 0; precedence < 10; precedence++)
            {
                if (!runningPrecedenceDict.TryGetValue(precedence, out var runningPulsesInPrecedence)) continue;

				if (runningPulsesInPrecedence.Count == 0) { runningPrecedenceDict.Remove(precedence); continue; }

                List<Pulse> pulseList = new List<Pulse>(runningPulsesInPrecedence);


                bool newPulseShown = false;


                foreach (var pulse in pulseList)
                {
                    int framesBetweenStarting = (replacePulse.frameStarted - pulse.frameStarted) + replacePulse.totalFramesRun + pulse.totalFramesRun;

					Debug.Log(framesBetweenStarting);
					if ( ( framesBetweenStarting < pulse.totalFrameDuration || pulse.config.holdMode == PulseConfig.HoldMode.hold ) && !pulse._PULSEENDED_)
                    {
						Tracing.Add(pulse, Tracing.TraceType.Set, $"CachClr=Ended");
						pulse.config.cachedColour = replacePulse.config.cachedColour;


						pulse._FastForwardToFrame_(framesBetweenStarting);

                        _AddToActivePulses(pulse);

                        PulseHandler.New(pulse);
                        PulseHandler.HandlePulseAction(pulse);


						newPulseShown = true;
                        break;

                    }
                    else
                    {
                        _RemoveFromRunningPulses(pulse);

                        if (runningPulsesInPrecedence.Count == 0) runningPrecedenceDict.Remove(precedence);

                        pulse._PULSEENDED_ = true; DebugInfo.pulsesRunning -= 1;
                    }
                }
                if (newPulseShown) break;

            }

        }


        static void _ClearExpiredPulses()
        {
            List<Pulse> expiredPulsesDeleteQueue = new List<Pulse>();

            foreach (var pulsePositionPrecedenceDict in pulsesRunningTrack.Values)
            {
                foreach (var precedence in pulsePositionPrecedenceDict.Values)
                {
                    foreach (var pulse in precedence)
                    {

                        if (totalTickTimer - pulse.frameStarted >= pulse.totalFrameDuration && (pulse.config.holdMode == PulseConfig.HoldMode.noHold || pulse._PULSEENDED_)) { expiredPulsesDeleteQueue.Add(pulse); DebugInfo.pulsesRunning -= 1; }

                    }
                }
            }

            foreach (var expiredPulse in expiredPulsesDeleteQueue)
            {
                _RemoveFromRunningPulses(expiredPulse);

                if (pulsesRunningTrack.Count == 0) pulsesRunningTrack[expiredPulse.config.position].Remove(expiredPulse.config.precedence);

                DebugInfo.pulsesRunning -= 1;
            }
        }



        public static void SchedulePulse(Pulse pulse, int startInXTicks)
        {

            _PAUSECYCLE_ = true;

            PulseConfig config = pulse.config;
            int startOnTick = (totalTickTimer + 1) + startInXTicks;

			Tracing.Add(pulse, Tracing.TraceType.Schedule, $"OnTick={totalTickTimer}-ForTick={startOnTick}");

			pulsesScheduledTrack.TryAdd(startOnTick, new Dictionary<int, List<Pulse>>(10));
            pulsesScheduledTrack[startOnTick].TryAdd(config.precedence, new List<Pulse>(48));

            pulsesScheduledTrack[startOnTick][config.precedence].Add(pulse);

            _PAUSECYCLE_ = false;

        }

        public static void SchedulePulseGroup(PulseGroup pulseGroup, int startInXTicks)
        {

            _PAUSECYCLE_ = true;

            GroupConfig config = pulseGroup.groupConfig;
            int startOnTick = (totalTickTimer + 1) + startInXTicks;

            pulseGroupsScheduledTrack.TryAdd(startOnTick, new Dictionary<int, List<PulseGroup>>(10));
            pulseGroupsScheduledTrack[startOnTick].TryAdd(config.precedence, new List<PulseGroup>(32));

            pulseGroupsScheduledTrack[startOnTick][config.precedence].Add(pulseGroup);

            _PAUSECYCLE_ = false;

        }

        public static void SchedulePulseGroupGroup(PulseGroupGroup pulseGroupGroup, int startInXTicks)
        {

            _PAUSECYCLE_ = true;

            GroupConfig config = pulseGroupGroup.groupConfig;
            int startOnTick = (totalTickTimer + 1) + startInXTicks;

            pulseGroupGroupsScheduledTrack.TryAdd(startOnTick, new Dictionary<int, List<PulseGroupGroup>>(10));
            pulseGroupGroupsScheduledTrack[startOnTick].TryAdd(config.precedence, new List<PulseGroupGroup>(8));

            pulseGroupGroupsScheduledTrack[startOnTick][config.precedence].Add(pulseGroupGroup);

            _PAUSECYCLE_ = false;                                                                                                   
            
        }





        static void _RemoveFromRunningPulses(Pulse pulse)
        {
			Tracing.Add(pulse, Tracing.TraceType.Remove, $"runningPulses");

            if (!pulsesRunningTrack.ContainsKey(pulse.config.position) || !pulsesRunningTrack[pulse.config.position].ContainsKey(pulse.config.precedence)) return;

            if (pulsesRunningTrack[pulse.config.position][pulse.config.precedence].Contains(pulse)) { pulsesRunningTrack[pulse.config.position][pulse.config.precedence].Remove(pulse); }

            else { Tracing.Add(pulse, Tracing.TraceType.Error, $"PulseNotRunning"); }


		}
        static void _RemoveFromActivePulses(Pulse pulse)
        {
			Tracing.Add(pulse, Tracing.TraceType.Remove, $"activePulses");
			if (pulsesActiveTrack.ContainsKey(pulse.config.position)) { pulsesActiveTrack.Remove(pulse.config.position); }

            else { Tracing.Add(pulse, Tracing.TraceType.Error, $"PulseNotActive"); }
		}
		static void _RemoveFromHoldingPulses(Pulse pulse)
		{
			Tracing.Add(pulse, Tracing.TraceType.Remove, $"holdingPulses");

			if (holdingPulses.Contains(pulse)) { holdingPulses.Remove(pulse); }

			else { Tracing.Add(pulse, Tracing.TraceType.Error, $"PulseNotHolding"); }
		}


        static void _AddToHoldingPulses(Pulse pulse)
        {
            Tracing.Add(pulse, Tracing.TraceType.Add, $"holdingPulses");

            if (!holdingPulses.Contains(pulse)) { holdingPulses.Add(pulse); }

			else { Tracing.Add(pulse, Tracing.TraceType.Error, $"PulseAlreadyHolding"); }
		}


		static void _AddToActivePulses(Pulse pulse)
        {
            if (!pulsesActiveTrack.ContainsKey(pulse.config.position)) { pulsesActiveTrack.Add(pulse.config.position, pulse); Tracing.Add(pulse, Tracing.TraceType.Add, $"activePulses"); }

            else { Tracing.Add(pulse, Tracing.TraceType.Error, $"PulseAlreadyActive"); }
		}
        static void _AddToRunningPulses(Pulse pulse)
        {
			Tracing.Add(pulse, Tracing.TraceType.Add, $"runningPulses");

			pulsesRunningTrack.TryAdd(pulse.config.position, new Dictionary<int, List<Pulse>>(10));
            pulsesRunningTrack[pulse.config.position].TryAdd(pulse.config.precedence, new List<Pulse>(8));

            if (!pulsesRunningTrack[pulse.config.position][pulse.config.precedence].Contains(pulse)) { pulsesRunningTrack[pulse.config.position][pulse.config.precedence].Add(pulse); }

            else { Tracing.Add(pulse, Tracing.TraceType.Error, $"PulseAlreadyRunning"); }
        }


		

        public static void EndAllPulsesOf(Color? pulseColour, Vector2? pos)
        {
            List<Pulse> pulseDeleteQueue = new List<Pulse>();

			bool bothPulseColourAndPosition = (pulseColour is not null && pos is not null);

			foreach (var tick in pulsesScheduledTrack)
            {
                foreach (var precedence in tick.Value)
                {
                    foreach (var pulse in precedence.Value)
                    {
                        if ( ( pulse.config.pulseColour == pulseColour && pulse.config.position == pos && bothPulseColourAndPosition ) || ( (pulse.config.pulseColour == pulseColour || pulse.config.position == pos) && !bothPulseColourAndPosition ) )
                        {
                            pulseDeleteQueue.Add(pulse);
                        }
                    }
                }
            }
            
            foreach (var tick in pulseGroupsScheduledTrack)
            {
                foreach (var precedence in tick.Value)
                {
                    foreach(var group in precedence.Value)
                    {
                        foreach(var pulse in group.GetPulses())
                        {
                            if ((pulse.config.pulseColour == pulseColour && pulse.config.position == pos && bothPulseColourAndPosition) || ((pulse.config.pulseColour == pulseColour || pulse.config.position == pos) && !bothPulseColourAndPosition))
							{
                                pulseDeleteQueue.Add(pulse);
                            }
                        }
                    }
                }
            }

			foreach (var tick in pulseGroupGroupsScheduledTrack)
			{
				foreach (var precedence in tick.Value)
				{
					foreach (var groupGroup in precedence.Value)
					{
						foreach (var group in groupGroup.GetPulseGroups())
						{
							foreach (var pulse in group.GetPulses())
							{
								if ((pulse.config.pulseColour == pulseColour && pulse.config.position == pos && bothPulseColourAndPosition) || ((pulse.config.pulseColour == pulseColour || pulse.config.position == pos) && !bothPulseColourAndPosition))
								{
									pulseDeleteQueue.Add(pulse);
								}
							}
						}
					}
				}
			}
            foreach (var position in pulsesRunningTrack)
            {
                foreach (var precedence in position.Value)
                {
                    foreach (var pulse in precedence.Value)
                    {
                        if ((pulse.config.pulseColour == pulseColour && pulse.config.position == pos && bothPulseColourAndPosition) || ((pulse.config.pulseColour == pulseColour || pulse.config.position == pos) && !bothPulseColourAndPosition))
						{
                            pulseDeleteQueue.Add(pulse);
                        }
                    }
                }
            }
			foreach (var pulse in pulsesActiveTrack.Values)
			{
				if ((pulse.config.pulseColour == pulseColour && pulse.config.position == pos && bothPulseColourAndPosition) || ((pulse.config.pulseColour == pulseColour || pulse.config.position == pos) && !bothPulseColourAndPosition))
				{
					pulseDeleteQueue.Add(pulse);
				}
			}

			foreach (var pulse in pulseDeleteQueue) {
                //PulseActions.PulseEnded(pulse);
                pulse._PULSEENDED_ = true;
				//if (pulse.config.cachedColour is not null) pulse.square.color = (Color)pulse.config.cachedColour;
            }
		}


	}
    //

    public static String ColorToFormattedString(Color32 color)
    {
        for (int i = 0; i<4; i++) { color[i] = (byte)Math.Round((float)color[i], 0); }
        return $"(r{color.r},g{color.g},b{color.b},a{color.a})";
    }

	static void _MousePressedBoard(Vector2 squarePressed)
	{
		//Debug.Log($"{squarePressed.ToString()} pressed");

		Piece piece;
		var board = storage.board;

		bool isAPieceOnClickedSquare = game.pieceBoard.TryGetValue(squarePressed, out piece);

		int pulsePrecedence = 0;
		Color guiClickColour = new Color(0, 0, 0, 0);

		Color guiResetToColour = new Color(0, 0, 0, 0);
		PulseConfig.HoldMode holdMode = PulseConfig.HoldMode.noHold;
		PulseConfig.HoldEndCondition holdEndCondition = PulseConfig.HoldEndCondition.whenInterruptedByAnyPrecedence;

		int guiLerpFrames = 30;
		int guiHoldFrames = 12;
		int guiPulseRunTimes = 1;

		//a piece is selected and player clicks on the selected piece | action: deselect piece
		if (pieceSelected && piece == pieceSelected)
		{
			Scheduler.EndAllPulsesOf(board.selectColour, pieceSelected.position);

			guiClickColour = board.unSelectColour;
			guiLerpFrames = 25; guiHoldFrames = 15; guiPulseRunTimes = 1; pulsePrecedence = 0;

			pieceSelected = null;
			Scheduler.EndAllPulsesOf(board.legalMovesSelectFlashingColour, null); 
            Scheduler.EndAllPulsesOf(board.legalMovesUnSelectFlashingColour, null);
			GUIDrawLegalSquares();
		}

		//a piece is selected and player clicks on an enemy piece or empty square | action: check if it is a legal move; and if so- make move, else error pulse
		else if (pieceSelected && piece?.colour != game.turnColour)
		{
			if (game.IsLegalMove(pieceSelected, squarePressed))
			{

				Scheduler.EndAllPulsesOf(board.legalMovesSelectFlashingColour, null); Scheduler.EndAllPulsesOf(board.legalMovesUnSelectFlashingColour, null); 
                Scheduler.EndAllPulsesOf(board.madeMoveColour, null); Scheduler.EndAllPulsesOf(board.selectColour, pieceSelected.position); Scheduler.EndAllPulsesOf(board.unSelectColour, pieceSelected.position);

				Pulse currentSquarePulse = new Pulse(new PulseConfig(pieceSelected.position, board.madeMoveColour, board.madeMoveColour / 1.3f, 3, 40, 40, 1, PulseConfig.HoldMode.hold, PulseConfig.HoldEndCondition.whenNextTurn));
				Scheduler.SchedulePulse(currentSquarePulse, 0);

				Pulse pulse = new Pulse(new PulseConfig(squarePressed, board.madeMoveColour, board.madeMoveColour / 1.15f, 3, 40, 80, 1, PulseConfig.HoldMode.hold, PulseConfig.HoldEndCondition.whenNextTurn));
				Scheduler.SchedulePulse(pulse, 0);

				
				game.MakeMove(pieceSelected, squarePressed, false, false);

				pieceSelected = null;
				return;
			}
			else
			{
				guiClickColour = board.errorColour; guiPulseRunTimes = 3; guiLerpFrames = 5; guiHoldFrames = 5;
				Scheduler.SchedulePulse(new Pulse(new PulseConfig(pieceSelected.position, board.unSelectColour, 0, 15, 25, 1)), 0);

				pieceSelected = null;
                //Scheduler.EndAllPulsesOf(board.legalMovesSelectFlashingColour, null); 
                //Scheduler.EndAllPulsesOf(board.legalMovesUnSelectFlashingColour, null);
				GUIDrawLegalSquares();
			}
		}

		//a piece is clicked and it is a friendly piece | action: select piece
		else if (isAPieceOnClickedSquare && piece?.colour == game.turnColour)
		{

			if (pieceSelected)
			{
				Scheduler.EndAllPulsesOf(board.selectColour, pieceSelected.position); Scheduler.EndAllPulsesOf(board.unSelectColour, pieceSelected.position); //Scheduler.EndAllPulsesOf(board.legalMovesSelectFlashingColour, null); Scheduler.EndAllPulsesOf(board.legalMovesUnSelectFlashingColour, null);
				Scheduler.SchedulePulse(new Pulse(new PulseConfig(pieceSelected.position, board.unSelectColour, 0, 15, 25, 1)), 0);
			}

			guiClickColour = board.selectColour;

			guiResetToColour = board.selectedColour;
			holdMode = PulseConfig.HoldMode.hold;
			holdEndCondition = PulseConfig.HoldEndCondition.whenInterruptedBySamePrecedence;
			pulsePrecedence = 0;

			guiLerpFrames = 25; guiPulseRunTimes = 1; guiHoldFrames = 20;

            pieceSelected = piece;

			
            Scheduler.EndAllPulsesOf(board.unSelectColour, pieceSelected.position);
			Scheduler.EndAllPulsesOf(board.selectColour, pieceSelected.position);
			GUIDrawLegalSquares();
		}

		//none of the above are true ie: clicking on an enemy piece without a piece selected or clicking on an empty square without a piece selected | action: error pulse on clicked square
		else
		{

            if (pieceSelected) 
            {
				Scheduler.EndAllPulsesOf(board.selectColour, pieceSelected.position);
				Scheduler.SchedulePulse(new Pulse(new PulseConfig(pieceSelected.position, board.unSelectColour, 0, 15, 25, 1)), 0);

				Scheduler.EndAllPulsesOf(board.legalMovesSelectFlashingColour, null); Scheduler.EndAllPulsesOf(board.legalMovesUnSelectFlashingColour, null);

				GUIDrawLegalSquares();
			}

			guiClickColour = board.errorColour;
			guiPulseRunTimes = 3; guiLerpFrames = 5; guiHoldFrames = 5;
			pulsePrecedence = 0;
			pieceSelected = null;
		}

		if (guiClickColour.a != 0)
		{
			if (guiResetToColour.a == 0) { Scheduler.SchedulePulse(new Pulse(new PulseConfig(squarePressed, guiClickColour, pulsePrecedence, guiHoldFrames, guiLerpFrames, guiPulseRunTimes)), 0); }

			else { Scheduler.SchedulePulse(new Pulse(new PulseConfig(squarePressed, guiClickColour, guiResetToColour, pulsePrecedence, guiHoldFrames, guiLerpFrames, guiPulseRunTimes, holdMode, holdEndCondition)), 0); }
		}
	}



    //
	//
	static class PulseHandler
    {
        public static void New(Pulse pulse)
        {
            PulseConfig config = pulse.config;

            pulse.config.cachedColour = pulse.config.cachedColour is null ? storage.board.squares[pulse.config.position].material.color : pulse.config.cachedColour;


            if (config.resetColour is null) { config.resetColour = pulse.config.cachedColour; }
            else { config.resetColour = Color.Lerp((Color) config.resetColour, (Color) config.cachedColour, 0.2f); }


            DebugInfo.pulsesRunning += 1;

            //config.pulseColour = Color.Lerp(config.pulseColour, (Color) config.cachedColour, 0.3f);
        }

        public static void HandlePulseAction(Pulse pulse)
        {
            PulseConfig config = pulse.config;

            if ( pulse.fadeFramesRunInIteration < Settings.Pulsing.fadeInXFrames ) { 
                PulseActions.Fade(pulse); 

            }

            else if ( pulse.holdFramesRunInIteration < config.holdColourForXFramesInIteration ) {
				Tracing.Add(pulse, Tracing.TraceType.Action, $"hold={pulse.holdFramesRunInIteration}/{pulse.config.holdColourForXFramesInIteration}");

				pulse.holdFramesRunInIteration += 1; pulse.totalFramesRun += 1; 

            }

            else if ( pulse.lerpFramesRunInIteration < config.lerpInXFramesInIteration ) { 
                PulseActions.Lerp(pulse); 

            }

            else if ( pulse.iteration < config.runXIterations ) {
				Tracing.Add(pulse, Tracing.TraceType.Action, $"iter={pulse.iteration}/{pulse.config.runXIterations}");

				pulse.fadeFramesRunInIteration = 0; pulse.holdFramesRunInIteration = 0; pulse.lerpFramesRunInIteration = 0;

                pulse.totalFramesRun += 1; pulse.iteration += 1; 

            }

            else if ( pulse.config.holdMode == PulseConfig.HoldMode.hold )
            {
				if (!Scheduler.holdingPulses.Contains(pulse))
				{
					Tracing.Add(pulse, Tracing.TraceType.Add, $"holdPulsesDict");

					Scheduler.holdingPulses.Add(pulse);
				}
                pulse.totalFramesRun++;

			}

            else {
                PulseActions.PulseEnded(pulse);

            }
        }
    }
    //
    //
    //
    static class PulseActions
    {

        //Smoothly fade in between square's current colour and pulse.pulseColour
        public static void Fade(Pulse pulse)
        {
			Tracing.Add(pulse, Tracing.TraceType.Action, $"fade={pulse.fadeFramesRunInIteration}/{Settings.Pulsing.fadeInXFrames}");

			pulse.fadeFramesRunInIteration += 1;


            Color transparentPulseColor = Color.Lerp(pulse.config.pulseColour, (Color)pulse.config.cachedColour, 0.1f);

			pulse.square.color = Color.Lerp(pulse.square.color, transparentPulseColor, ((float)pulse.fadeFramesRunInIteration / (float)Settings.Pulsing.fadeInXFrames));

            pulse.totalFramesRun += 1;
        }


        //Smoothly fade between pulseColour and resetColour
        public static void Lerp(Pulse pulse)
        {
			PulseConfig config = pulse.config;

			Tracing.Add(pulse, Tracing.TraceType.Action, $"lerp={pulse.lerpFramesRunInIteration}/{config.lerpInXFramesInIteration}");

            pulse.lerpFramesRunInIteration += 1;


			Color transparentPulseColor = Color.Lerp(pulse.config.pulseColour, (Color)pulse.config.cachedColour, 0.1f);

			pulse.square.color = Color.Lerp(transparentPulseColor, (Color)config.resetColour, (float)pulse.lerpFramesRunInIteration / (float)config.lerpInXFramesInIteration);

            pulse.totalFramesRun += 1;
		}

        //Runs when all iterations have run
        public static void PulseEnded(Pulse pulse)
        {
			Tracing.Add(pulse, Tracing.TraceType.End);

			pulse._PULSEENDED_ = true;
            pulse.square.color = (Color)pulse.config.cachedColour;

			pulse.totalFramesRun += 1;
			DebugInfo.pulsesRunning -= 1;
		}
    }
    //
    //
    //


    public static void GUIUPDATE()
    {
        switch (Settings.Debug.debugKey)
        {
            case KeyCode.W: DebugRefresh(Colour.White); break;
            case KeyCode.B: DebugRefresh(Colour.Black); break;
            case KeyCode.A: DebugRefresh(Colour.White); DebugRefresh(Colour.Black); break;
            //case KeyCode.None: storage.board.RefreshSquares(); break;
            default: break;
        }
    }

    static void DebugRefresh( Colour colour )
    {
        //GUIDrawAllControlledSquares(colour);

        foreach (var piece in game.GetPiecesOf(colour))
        {
            piece._resetGuiFlags();
            //piece.controlledSquaresChangeCache.Clear();

            piece.GUIPulseChangedControlledSquares();
        }
        GUIDrawCheckLines(colour); GUIDrawCheckLines(game.GetEnemyColour(colour));
        GUIDrawPinLines(colour);

        foreach (var dic in game.controlledSquares.Values)
        {
            foreach (var piece in dic)
            {
                controlledSquaresCache[piece.Key] = new HashSet<Vector2>(piece.Value);
            }
        }

    }


    //
    //
    //
    static class Tracing
    {
        class TracingDictionary<T> : Dictionary<T, string> {
            public TracingDictionary() : base() { }
            public TracingDictionary(int capacity) : base(capacity) { }
        }
        class TracingHistoryList<T> : List<(T tracedObject, string trace)>
        {

			public TracingHistoryList() : base() { }
            public TracingHistoryList(int capacity) : base(capacity) { }
        }




		static TracingDictionary<Pulse> pulseTraceDict = new TracingDictionary<Pulse>();

		static Dictionary<Vector2, TracingHistoryList<Pulse>> pPosTraceHistory = new Dictionary<Vector2, TracingHistoryList<Pulse>>(64);


		static Dictionary<Pulse, int> _pulseSchedulerTickCache = new Dictionary<Pulse, int>();


        public enum TraceType {
            None, Action, Schedule, Run, Start, Stop, Add, Error, Replace, Update, End, Remove, Delete, Set
        }

        public static class Settings {
            //TODO: stuff    
        }



        public static void Add(Pulse pulse, TraceType type, string message) { _addToPulseTrace(pulse, type, message); }
        public static void Add(Pulse pulse, TraceType type) { _addToPulseTrace(pulse, type, null); }

        public static string Get(Pulse pulse) { return pulseTraceDict.TryGetValue(pulse, out var trace) ? trace : "No Trace Found"; }

        public static (Pulse pulse, string trace)? GetTraceHistory(Vector2 position, int index) 
        {
            if (!pPosTraceHistory.ContainsKey(position)) return null;
            if (index >= pPosTraceHistory[position].Count) throw new Exception("Index Out Of Range");

            return pPosTraceHistory[position][index];
        }
        public static int GetTraceHistoryCount(Vector2 position) {
            if (!pPosTraceHistory.ContainsKey(position)) return 0;
            return pPosTraceHistory[position].Count;
		}



		static void _addToPulseTrace(Pulse pulse, TraceType type, string? message)
        {
            int tick = Scheduler.totalTickTimer;
            _InitTrace(pulse, tick);

			String trace = "";

            if (tick != _pulseSchedulerTickCache[pulse]) { trace += $" {tick}|"; _pulseSchedulerTickCache[pulse] = tick; }

            if (type != (int)TraceType.None) { trace += $"{_getDispName(type)}"; }
            if (type != (int)TraceType.None && message is not null) {trace += ":";}
			trace += message is null ? "|" : $"{message}|";


            _addToHistory(pulse, trace);

            _addToTrace(pulseTraceDict, pulse, trace);
		}

        static void _addToTrace<T>(TracingDictionary<T> dict, object key, string trace)
        {
            if (key is not T) throw new Exception($"<_addToTrace> Key ${key} does not match TypingDictionary Key {typeof(T)}");

            if (dict.ContainsKey((T)key)) dict[(T)key] += trace;
            else { dict.Add((T)key, trace); }
        }

		static void _InitTrace(Pulse pulse, int tick)
		{
			if (!pulseTraceDict.ContainsKey(pulse)) pulseTraceDict.Add(pulse, "");
			if (!_pulseSchedulerTickCache.ContainsKey(pulse)) _pulseSchedulerTickCache.Add(pulse, -1);
		}



        static void _addToHistory(Pulse pulse, string trace)
        {
			bool pulseIsActivePulse = _InitHistory(pulse, pulse.config.position);

			if (pulseIsActivePulse)
			{
                var history = pPosTraceHistory[pulse.config.position][GetTraceHistoryCount(pulse.config.position) - 1];

				pPosTraceHistory[pulse.config.position][GetTraceHistoryCount(pulse.config.position)-1] = (history.tracedObject, history.trace + trace);
			}
		}

		static bool _InitHistory(Pulse pulse, Vector2 position)
		{
			if (!pPosTraceHistory.ContainsKey(position)) pPosTraceHistory.Add(position, new TracingHistoryList<Pulse>());

            if (!Scheduler.pulsesActiveTrack.ContainsKey(position)) return false;
                    

            if (Scheduler.pulsesActiveTrack[position].Equals(pulse))
            {

				if (pPosTraceHistory[position].Count == 0) { pPosTraceHistory[position].Add((pulse, Get(pulse) + "<color=#f5ffc7>")); return true; }


			    if (pPosTraceHistory[position][GetTraceHistoryCount(position)-1].tracedObject.Equals(pulse)) return true;
                else 
                {
					var previous = pPosTraceHistory[position][GetTraceHistoryCount(position) - 1];
                    pPosTraceHistory[position][GetTraceHistoryCount(position) - 1] = (previous.tracedObject, previous.trace + "</color>");


					pPosTraceHistory[position].Add((pulse, Get(pulse) + "<color=#f5ffc7>"));
                    return true;
                }
            } 
            else { return false; }
		}

		static string _getDispName(TraceType type)
        {
            return type switch
            {
                TraceType.Action => "ACT",
                TraceType.Schedule => "SCHD",
                TraceType.Run => "RUN",
                TraceType.Start => "STRT",
                TraceType.Stop => "STOP",
                TraceType.Add => "ADD",
                TraceType.Error => "ERR",
                TraceType.Replace => "RPLC",
                TraceType.Update => "UPD",
                TraceType.End => "END",
                TraceType.Delete => "DELT",
                TraceType.Remove => "REMV",
                TraceType.Set => "SET",
            };
        }
	}
    //
    //
    //





    public static void MoveNotation(string moveString)
    {
        storage.movestext.text += moveString;

        moveNotations.TryAdd(game.move, new string[2]);
        moveNotations[game.move][game.turnColour == Colour.White ? 0 : 1] = moveString;
    }

    static List<Vector2> guiSelectedSquaresCache = new List<Vector2>(64);

    static Dictionary<int, HashSet<Vector2>> guiControlledSquaresCache = new Dictionary<int, HashSet<Vector2>>(64);
    static Piece pieceSelectedCache = null;

    static void GUIUnselectLegalSquares()
    {
        PulseGroup unSelectPulses = new PulseGroup(96, new GroupConfig(0, 0, 0));
        foreach (var Tile in guiSelectedSquaresCache)
        {
            if (Tile == pieceSelected?.position) { continue; }
            unSelectPulses.Add(new Pulse(new PulseConfig( Tile, storage.board.legalMovesUnSelectFlashingColour, 1, 6, 6, 2 )));
        }
        Scheduler.SchedulePulseGroup(unSelectPulses, 0);
    }

    public static void GUIDrawLegalSquares()
    {
        if ((pieceSelected?.colour == Colour.White && !Settings.Draw.drawWhiteSelectedPieceLegalSquares) || (pieceSelected?.colour == Colour.Black && !Settings.Draw.drawBlackControlledSquares)) return;

        var board = storage.board;

        //Debug.Log($"selectedPiece:{pieceSelected?.id}, {cachedMove}, cachedSelectedPiece:{pieceSelectedCache?.id}");


        if (pieceSelectedCache == pieceSelected) GUILoadCache();


        if (pieceSelectedCache != pieceSelected)
        {
			GUIUnselectLegalSquares();

			guiSelectedSquaresCache.Clear();
            pieceSelectedCache = pieceSelected;

            if (pieceSelected)
            {
                pieceSelectedCache = pieceSelected;
                PulseGroup selectPulses = new PulseGroup(96, new GroupConfig(1, 5, 0));

                foreach (var pos in game.legalMoves[pieceSelected])
                {
                    guiSelectedSquaresCache.Add(pos);

                    Pulse legalSquaresPulse = new Pulse(new PulseConfig(pos, board.legalMovesSelectFlashingColour, storage.board.legalMovesColour, 1, 20, 20, 1, PulseConfig.HoldMode.hold, PulseConfig.HoldEndCondition.whenInterruptedBySamePrecedence));

                    selectPulses.Add(legalSquaresPulse);

                }
                Scheduler.SchedulePulseGroup(selectPulses, 0);
            }
        }
    }

    static void GUILoadCache()
    {
        PulseGroup selectPulses = new PulseGroup(96, new GroupConfig(1, 5, 0));
        foreach (var Tile in guiSelectedSquaresCache)
        {
            Pulse legalSquaresPulse = new Pulse(new PulseConfig( Tile, storage.board.legalMovesSelectFlashingColour, storage.board.legalMovesColour, 1, 20, 20, 1, PulseConfig.HoldMode.hold, PulseConfig.HoldEndCondition.whenInterruptedBySamePrecedence));

            selectPulses.Add(legalSquaresPulse);
        }
        Scheduler.SchedulePulseGroup(selectPulses, 0);
    }

    //static void GUILegalMovesReset()
    //{
    //    var cSquares = GetControlledSquares(turnColour);

    //    foreach (var Tile in guiSelectedSquaresCache)
    //    {
    //        if (cSquares.Contains(Tile.Key)) continue;
    //        else
    //        {
    //            Pulse pulse = new Pulse(Tile.Key, storage.board.GetSquareColor(Tile.Key), 0, 0, 1);
    //            pulse.resetColour = storage.board.GetSquareColor(Tile.Key); pulse.cachedColour = storage.board.GetSquareColor(Tile.Key); pulse.fadeInEnabled = false;
    //            GUIPulseSquare(pulse);
    //        }
    //    }
    //    guiSelectedSquaresCache.Clear();
    //    pieceSelectedCache = null;
    //}

    public static void GUIDrawPieceControlledSquares( Piece piece )
    {
        if (((piece.colour == Colour.White && !Settings.Draw.drawWhiteControlledSquares) || (piece.colour == Colour.Black && !Settings.Draw.drawBlackControlledSquares)) || Settings.Debug.debugKey == KeyCode.None) return;

        if (!guiControlledSquaresCache.ContainsKey(piece.id)) { guiControlledSquaresCache.TryAdd(piece.id, new HashSet<Vector2>(96)); }


        var board = storage.board;

        Color flashingColor = piece.colour == Colour.White ? board.whiteCSFlashingColour : board.blackCSFlashingColour;
        Color controlledSquaresColor = piece.colour == Colour.White ? board.whiteControlledSquaresColour : board.blackControlledSquaresColour;

        if (changedPiecesMoves.TryGetValue(piece.id, out var dict))
        {
            var groupedDepthPulses = new PulseGroupGroup(8, new GroupConfig(3, 8, 0));

            for (int i = 0; i < dict.Count; i++)
            {
                PulseGroup depthPulses = new PulseGroup(96, new GroupConfig(2, 0, 0));
                foreach (var directionPos in dict[i])
                {

                    //guiControlledSquaresCache[piece.id].Add(pos);
                    depthPulses.Add(new Pulse(new PulseConfig( directionPos, flashingColor, 2, 10, 25, 1 )));
                }
                groupedDepthPulses.Add(depthPulses);
            }
            Scheduler.SchedulePulseGroupGroup(groupedDepthPulses, 0);
        }
    }

    public static void GUIDrawCheckLines( Colour colour )
    {
        var king = game.GetPiecesOf(Piece.Type.King, colour)[0];

        var checkLines = game.GetCheckLines(colour);

        foreach (var checkLine in checkLines)
        {
            if (checkLine.checkingPiece.colour == colour) continue;


            var pulseGroup = new PulseGroup(checkLine.lineVectors.Count, new GroupConfig(3, 10, 0));

            foreach (var pos in checkLine.lineVectors)
            {
                bool isKingSquare = game.GetPieceAtPosition(pos)?.type == Piece.Type.King;

                Pulse pulse = new Pulse(new PulseConfig(pos, storage.board.checkLineColour, 1, 30, 100, 1));

                if (isKingSquare)
                {
                    pulse.config.pulseColour = new Color(1f, 0f, 0f);
                }

                pulseGroup.Add(pulse);
            }
            Scheduler.SchedulePulseGroup(pulseGroup, 0);

        }
    }
    public static void GUIDrawPinLines( Colour colour )
    {
        var king = game.GetPiecesOf(Piece.Type.King, colour)[0];

        foreach (var pinnedPiece in game.pinLines)
        {
            if (pinnedPiece.Key.colour == colour || pinnedPiece.Key.type == Piece.Type.Pawn || pinnedPiece.Key.type == Piece.Type.King) continue;


            var pulseGroupGroup = new PulseGroupGroup(pinnedPiece.Value.Count, new GroupConfig(4, 25, 0));

            foreach (var pinnedToPiece in pinnedPiece.Value)
            {
                var pulseGroup = new PulseGroup(pinnedToPiece.Value.Count, new GroupConfig(4, 7, 0));
                if (pinnedToPiece.Key.type == Piece.Type.Pawn) continue;


                foreach (var pos in pinnedToPiece.Value)
                {
                    Pulse pulse = new Pulse(new PulseConfig(pos, storage.board.pinLineColour, 2, 40, 60, 1));

                    //square the piece at end of pin is on
                    if (pos == pinnedToPiece.Key.position)
                    {
                        pulse.config.pulseColour = storage.board.pinLineColour / 2f + new Color(1f, 0f, 0);
                    }

                    //square pinned piece is on
                    if (pos == pinnedPiece.Key.position)
                    {
                        pulse.config.pulseColour = storage.board.pinLineColour * 1.5f + new Color(1f, 0.1f, 0);
                    }

                    pulseGroup.Add(pulse);
                }
                pulseGroupGroup.Add(pulseGroup);
            }
            Scheduler.SchedulePulseGroupGroup(pulseGroupGroup, 0);
        }
    }

    public static void GUIDrawAllControlledSquares( Colour colour )
    {
        var board = storage.board;

        foreach (var square in game.GetControlledSquares(colour))
        {
            var squareColour = colour == Colour.Black ? board.GetSquareColor(square) + board.blackControlledSquaresColour : board.GetSquareColor(square) + board.whiteControlledSquaresColour;

            if (!Scheduler.pulsesActiveTrack.ContainsKey(square)) { board.squares[square].material.color = squareColour; }

            else 
            {
                var pulseConfig = Scheduler.pulsesActiveTrack[square].config;

                pulseConfig.cachedColour = squareColour; 
            }
        }
    }

    public static void GUIDrawLine( HashSet<Vector2> line, Color colour )
    {
        if (line is null) { Debug.Log("GUIDrawLine ERROR 1, line is null"); return; }
        var boardSquares = storage.board.squares;
        foreach (var position in line)
        {
            boardSquares[position].material.color = colour;
        }
    }


    public static async void GUIEndTurn(Piece pieceToMove, Vector2 position, float lerpSeconds)
    {
        Vector2 cachedPosition = new Vector2(pieceToMove.gameObject.transform.position.x, pieceToMove.gameObject.transform.position.y);

        for (int tick = 1; tick < (lerpSeconds * Scheduler.ticksPerSecond); tick++)
        { 
            pieceToMove.gameObject.transform.position = Vector2.Lerp(cachedPosition, position, Math.Min((tick / (Scheduler.ticksPerSecond * lerpSeconds)), 1));

            float delayCalc = ((1 / Scheduler.ticksPerSecond) * 1000);

            await Task.Delay((int)((1 / Scheduler.ticksPerSecond) * 1000));
        }

        pieceToMove.gameObject.transform.position = pieceToMove.position;

        pieceSelected = null;
        while (DebugInfo.pieceCalculationsRunning != 0) { await Task.Delay(1); }

        guiSelectedSquaresCache.Clear();



        HashSet<Pulse> deleteQueue = new HashSet<Pulse>(16);
        foreach (var pulse in Scheduler.holdingPulses)
        {
            if (pulse.config.holdEndCondition == PulseConfig.HoldEndCondition.whenNextTurn)
            {
                pulse._PULSEENDED_ = true;
                pulse.square.color = (Color)pulse.config.cachedColour;
                Scheduler.pulsesActiveTrack.Remove(pulse.config.position);
                deleteQueue.Add(pulse);
            }
        }
        foreach (var pulse in deleteQueue) { Scheduler.holdingPulses.Remove(pulse); }



        pieceSelectedCache = null;

        game._EndTurn();
    }
    //
    //
    //




    //public void GUIDrawCheckLines(Colour pieceColour)
    //{

    //    Color checkLineColor = new Color(-0.2f, 0.5f, 0.5f);

    //    var checkLines = GetCheckLines(pieceColour);

    //    foreach (var checkLine in checkLines)
    //    {
    //        GUIDrawLine(checkLine.lineVectors, checkLineColor);
    //    }
    //}
}
