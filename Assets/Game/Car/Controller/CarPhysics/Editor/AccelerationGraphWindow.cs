#if UNITY_EDITOR
using System.Collections.Generic;
using Car.Controller;
using Car.Controller.CarPhysics.Drivetrain;
using Car.Controller.CarPhysics.Engine;
using Car.Controller.CarPhysics.Transmission;
using Common.Runtime;
using UnityEditor;
using UnityEngine;

namespace Car.Controller.CarPhysics.Editor
{
	public class AccelerationGraphWindow : EditorWindow
	{
		private const int   CurveSamples  = 256;

		private const float KmhPerMs = 3.6f;

		private const float DefaultMinSpeedKmh = -1f;
		private const float DefaultMaxSpeedKmh = 252f;
		private const float DefaultYMin = -1f;
		private const float DefaultYMax = 50f;

		private EngineSO       _engineSO;
		private TransmissionSO _transmissionSO;
		private DrivetrainSO   _drivetrainSO;
		private CarPhysicsData _carPhysicsData;

		private List<CurveData> _cachedCurves;
		private float _cachedMaxRpm;
		private int   _builtKey;

		private enum PlotMode { SelectedGear, AllGears }
		private PlotMode _plotMode = PlotMode.AllGears;

		private int   _selectedGearIndex = 0;
		private float _minSpeed = DefaultMinSpeedKmh / KmhPerMs;
		private float _maxSpeed = DefaultMaxSpeedKmh / KmhPerMs;

		private bool _autoY = true;
		private float _yMin = DefaultYMin;
		private float _yMax = DefaultYMax;

		private bool _showZeroLines = true;
		private bool _showLegend = true;
		private bool _showCursorReadout = true;

		[MenuItem("Tools/ArcadeCar/Acceleration Graph")]
		public static void ShowWindow()
		{
			var wnd = GetWindow<AccelerationGraphWindow>();
			wnd.titleContent = new GUIContent("Accel Graph");
			wnd.Show();
		}

		private EngineService GetEvalEngine()
		{
			return _engineSO != null ? new EngineService(new EngineModel(_engineSO)) : null;
		}

		private void OnEnable()
		{
			Undo.postprocessModifications += OnUndoModifications;
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
			EditorApplication.projectChanged += OnProjectChanged;
		}

		private void OnDisable()
		{
			Undo.postprocessModifications -= OnUndoModifications;
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
			EditorApplication.projectChanged -= OnProjectChanged;
		}

		private void OnProjectChanged()
		{
			Repaint();
		}

		private UndoPropertyModification[] OnUndoModifications(UndoPropertyModification[] modifications)
		{
			Repaint();
			return modifications;
		}

		private void OnUndoRedoPerformed()
		{
			Repaint();
		}

		private void OnGUI()
		{
			var currentEvent = Event.current;
			if (_showCursorReadout && currentEvent != null &&
			    (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDrag))
			{
				Repaint();
			}

			EditorGUILayout.Space(4);
			EditorGUI.BeginChangeCheck();
			_engineSO       = (EngineSO)EditorGUILayout.ObjectField(new GUIContent("EngineSO", "Параметры двигателя"), _engineSO, typeof(EngineSO), false);
			_transmissionSO = (TransmissionSO)EditorGUILayout.ObjectField(new GUIContent("TransmissionSO", "Передачи и сцепление"), _transmissionSO, typeof(TransmissionSO), false);
			_drivetrainSO   = (DrivetrainSO)EditorGUILayout.ObjectField(new GUIContent("DrivetrainSO", "Колёса и шины"), _drivetrainSO, typeof(DrivetrainSO), false);
			_carPhysicsData = (CarPhysicsData)EditorGUILayout.ObjectField(new GUIContent("CarPhysicsData", "Общие настройки машины"), _carPhysicsData, typeof(CarPhysicsData), false);
			if (EditorGUI.EndChangeCheck())
			{
				ResetRangesToData();
			}

			if (_engineSO == null || _transmissionSO == null || _drivetrainSO == null)
			{
				EditorGUILayout.HelpBox("Укажите EngineSO, TransmissionSO и DrivetrainSO, чтобы увидеть график.", MessageType.Info);
				return;
			}

			DrawControls();
			EditorGUILayout.Space(6);

			var engine = GetEvalEngine();

			bool rebuilt;
			var curves = GetCurves(ComputeCacheKey(), engine, out rebuilt);
			if (curves == null || curves.Count == 0)
			{
				EditorGUILayout.HelpBox("Не удалось построить кривые. Проверьте массив Gears в TransmissionSO.", MessageType.Warning);
				return;
			}

			if (rebuilt)
				Repaint();

			float maxRpm = _cachedMaxRpm;

			if (_autoY)
			{
				float maxA = float.NegativeInfinity;
				foreach (var c in curves)
					maxA = Mathf.Max(maxA, c.MaxY);

				float pad = Mathf.Max(0.01f * Mathf.Max(1f, Mathf.Abs(maxA - _yMin)), 0.1f);
				_yMax = maxA + pad;

				if (Mathf.Approximately(_yMin, _yMax))
					_yMax = _yMin + 1f;
			}

			Rect rect = GUILayoutUtility.GetRect(position.width - 16f, Mathf.Max(220f, position.height - 220f));
			rect.x += 8f; rect.width -= 16f;

			DrawGraphBackground(rect);
			if (_showZeroLines)
				DrawZeroAxes(rect);

			foreach (var c in curves)
				DrawCurve(rect, c.Points, c.Color, 2f);

			Handles.color = EditorGUIUtility.isProSkin ? new Color(1,1,1,0.2f) : new Color(0,0,0,0.2f);
			Handles.DrawAAPolyLine(1.5f, new Vector3[]
			{
				new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin),
				new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax), new Vector3(rect.xMin, rect.yMin)
			});

			DrawAxisLabels(rect);
			if (_showLegend)
				DrawLegend(rect, curves);

			if (_showCursorReadout)
				DrawCursorReadout(rect, curves, maxRpm, engine);
		}

		private void DrawControls()
		{
			_plotMode = (PlotMode)GUILayout.Toolbar((int)_plotMode, new[] {"Selected gear", "All gears"}, GUILayout.Height(22));

			using (new EditorGUILayout.VerticalScope("box"))
			{
				int gearsCount = _transmissionSO.Gears != null ? _transmissionSO.Gears.Length : 0;

				if (_plotMode == PlotMode.SelectedGear)
				{
					_selectedGearIndex = Mathf.Clamp(EditorGUILayout.IntSlider(new GUIContent("Gear Index"), _selectedGearIndex, 0, Mathf.Max(0, gearsCount - 1)), 0, Mathf.Max(0, gearsCount - 1));
				}
				else
				{
					EditorGUILayout.LabelField("Gears:", $"0..{Mathf.Max(0, gearsCount - 1)}");
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					_minSpeed = EditorGUILayout.FloatField(new GUIContent("Speed Min", "км/ч"), _minSpeed * KmhPerMs) / KmhPerMs;
					_maxSpeed = EditorGUILayout.FloatField(new GUIContent("Speed Max", "км/ч"), _maxSpeed * KmhPerMs) / KmhPerMs;
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					_autoY = EditorGUILayout.Toggle(new GUIContent("Auto Y"), _autoY);
					_yMin = EditorGUILayout.FloatField(new GUIContent("Y Min"), _yMin);
					EditorGUI.BeginDisabledGroup(_autoY);
					_yMax = EditorGUILayout.FloatField(new GUIContent("Y Max"), _yMax);
					EditorGUI.EndDisabledGroup();
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					_showZeroLines = EditorGUILayout.Toggle(new GUIContent("Zero axes"), _showZeroLines);
					_showLegend = EditorGUILayout.Toggle(new GUIContent("Legend"), _showLegend);
					_showCursorReadout = EditorGUILayout.Toggle(new GUIContent("Cursor readout"), _showCursorReadout);
				}
			}
		}

		private void ResetRangesToData()
		{
			_selectedGearIndex = 0;
			_minSpeed = DefaultMinSpeedKmh / KmhPerMs;
			_maxSpeed = DefaultMaxSpeedKmh / KmhPerMs;
			_yMin = DefaultYMin;
			_yMax = DefaultYMax;
			_autoY = true;
			_cachedCurves = null;
		}

		private int ComputeCacheKey()
		{
			unchecked
			{
				int key = 17;
				key = key * 31 + HashAsset(_engineSO);
				key = key * 31 + HashAsset(_transmissionSO);
				key = key * 31 + HashAsset(_drivetrainSO);
				key = key * 31 + HashAsset(_carPhysicsData);
				key = key * 31 + _minSpeed.GetHashCode();
				key = key * 31 + _maxSpeed.GetHashCode();
				key = key * 31 + (int)_plotMode;
				key = key * 31 + _selectedGearIndex;
				return key;
			}
		}

		private static int HashAsset(Object asset)
		{
			if (asset == null) return 0;
			string json = EditorJsonUtility.ToJson(asset, false);
			return string.IsNullOrEmpty(json) ? 0 : json.GetHashCode();
		}

		private List<CurveData> GetCurves(int key, EngineService engine, out bool rebuilt)
		{
			if (_cachedCurves != null && key == _builtKey)
			{
				rebuilt = false;
				return _cachedCurves;
			}

			rebuilt = true;
			_cachedCurves = null;

			if (engine == null) return null;

			_cachedMaxRpm = engine.MaxRPM;
			_cachedCurves = BuildCurves(engine);

			_builtKey = ComputeCacheKey();

			return _cachedCurves;
		}

		private struct CurveData
		{
			public List<Vector3> Points;
			public float MaxY;
			public Color Color;
			public string Label;
			public int GearIndex;
		}

		private List<CurveData> BuildCurves(EngineService engine)
		{
			var list = new List<CurveData>();

			if (Mathf.Approximately(_maxSpeed, _minSpeed))
				_maxSpeed = _minSpeed + 1f;

			if (_transmissionSO == null || _transmissionSO.Gears == null) return list;

			int gearsCount = _transmissionSO.Gears.Length;
			if (gearsCount == 0) return list;

			if (_plotMode == PlotMode.SelectedGear)
			{
				int gear = Mathf.Clamp(_selectedGearIndex, 0, gearsCount - 1);
				var one = BuildCurveForGear(engine, gear, CurveSamples);
				if (one != null) list.Add(one.Value);
				return list;
			}

			for (int gi = 0; gi < gearsCount; gi++)
			{
				var one = BuildCurveForGear(engine, gi, CurveSamples);
				if (one != null) list.Add(one.Value);
			}

			return list;
		}

		private CurveData? BuildCurveForGear(EngineService engine, int gearIndex, int pointCount)
		{
			pointCount = Mathf.Max(2, pointCount);

			var pts = new List<Vector3>(pointCount);
			float maxA = float.NegativeInfinity;

			for (int i = 0; i < pointCount; i++)
			{
				float t = (float)i / (pointCount - 1);
				float speed = Mathf.Lerp(_minSpeed, _maxSpeed, t);
				float accel = EvaluateAccelInGear(engine, gearIndex, speed);

				pts.Add(new Vector3(speed, accel, 0f));
				maxA = Mathf.Max(maxA, accel);
			}

			Color col = _plotMode == PlotMode.SelectedGear
				? EditorGUIUtility.isProSkin ? new Color(0.5f, 0.9f, 1f, 1f) : new Color(0.1f, 0.4f, 0.8f, 1f)
				: Color.HSVToRGB(Mathf.Repeat(gearIndex * 0.13f, 1f), 0.8f, 0.95f);

			return new CurveData
			{
				Points    = pts,
				MaxY      = maxA,
				Color     = col,
				Label     = $"Gear {gearIndex} (x{GetRatioForGear(gearIndex):0.00})",
				GearIndex = gearIndex
			};
		}

		private float EvaluateAccelInGear(EngineService engine, int gearIndex, float speed)
		{
			if (engine == null || _engineSO == null || _transmissionSO == null
				|| _drivetrainSO == null || _carPhysicsData == null)
				return 0f;

			speed = Mathf.Max(0f, speed);

			float ratio        = GetRatioForGear(gearIndex);
			float radius       = _drivetrainSO.WheelRadius;
			float wheelInertia = _drivetrainSO.WheelInertia;
			float rollingFric  = _drivetrainSO.WheelInternalFriction;
			float effMass      = _carPhysicsData.BaseMass + engine.EngineMass;

			if (radius <= 0.0001f || wheelInertia <= 0.0001f || effMass <= 0.0001f) return 0f;

			float wheelOmega  = speed / radius;
			float engineOmega = wheelOmega * ratio;

			float idleThrottle = Mathf.Clamp01((engine.IdleOmega - engineOmega) * _engineSO.IdleThrottleGain);

			bool fuelCut = engineOmega >= engine.MaxOmega;
			float engineTorque = fuelCut ? 0f : engine.CalculateTorqueAt(Mathf.Clamp01(Mathf.Max(1f, idleThrottle)), engineOmega);

			float engineFriction = engine.CalculateFrictionAt(engineOmega);

			float capacityAtWheel = engine.PeakTorque * _transmissionSO.ClutchCoef * _transmissionSO.Efficiency * ratio;
			float demandedAtWheel = (engineTorque - engineFriction) * ratio * _transmissionSO.Efficiency;
			float driveTorque = Mathf.Abs(demandedAtWheel) <= capacityAtWheel
				? demandedAtWheel
				: Mathf.Clamp(demandedAtWheel, -capacityAtWheel, capacityAtWheel);

			float rollingTorque = wheelOmega * rollingFric;
			float drag          = Consts.AirDragForce(_carPhysicsData.AeroEfficiency, _carPhysicsData.FrontArea, speed);

			float tireForce = (effMass * radius * driveTorque - effMass * radius * rollingTorque + wheelInertia * drag)
			                / (effMass * radius * radius + wheelInertia);

			return (tireForce - drag) / effMass;
		}

		private float GetRatioForGear(int gearIndex)
		{
			var gears = _transmissionSO.Gears;
			if (gears == null || gears.Length == 0) return 1f;
			int i = Mathf.Clamp(gearIndex, 0, gears.Length - 1);
			return gears[i] * _transmissionSO.FinalGear;
		}

		private float GetRpmForSpeed(float speed, int gearIndex)
		{
			float radius = _drivetrainSO != null ? _drivetrainSO.WheelRadius : 0.35f;
			if (radius <= 0.0001f) return 0f;

			float wheelOmega = speed / radius;
			float engineOmega = wheelOmega * GetRatioForGear(gearIndex);
			return engineOmega * 30f / Mathf.PI;
		}

		private void DrawGraphBackground(Rect rect)
		{
			EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(0.95f, 0.95f, 0.95f, 1f));
		}

		private void DrawZeroAxes(Rect rect)
		{
			if (_minSpeed <= 0f && _maxSpeed >= 0f)
			{
				float x0 = Mathf.InverseLerp(_minSpeed, _maxSpeed, 0f);
				float x = Mathf.Lerp(rect.xMin, rect.xMax, x0);
				Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
				Handles.DrawAAPolyLine(1.5f, new Vector3[] { new Vector3(x, rect.yMin), new Vector3(x, rect.yMax) });
			}
			if (_yMin <= 0f && _yMax >= 0f)
			{
				float y0 = Mathf.InverseLerp(_yMin, _yMax, 0f);
				float y = Mathf.Lerp(rect.yMax, rect.yMin, y0);
				Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
				Handles.DrawAAPolyLine(1.5f, new Vector3[] { new Vector3(rect.xMin, y), new Vector3(rect.xMax, y) });
			}
		}

		private void DrawCurve(Rect rect, List<Vector3> data, Color color, float width)
		{
			if (data == null || data.Count < 2) return;

			var pts = STempScreenPoints;
			pts.Clear();
			for (int i = 0; i < data.Count; i++)
			{
				var p = data[i];
				float nx = Mathf.InverseLerp(_minSpeed, _maxSpeed, p.x);
				float ny = Mathf.InverseLerp(_yMin, _yMax, p.y);
				float sx = Mathf.Lerp(rect.xMin, rect.xMax, nx);
				float sy = Mathf.Lerp(rect.yMax, rect.yMin, ny);
				pts.Add(new Vector3(sx, sy, 0f));
			}

			Handles.BeginGUI();
			Handles.color = color;
			Handles.DrawAAPolyLine(width, pts.ToArray());
			Handles.EndGUI();
		}

		private static readonly List<Vector3> STempScreenPoints = new List<Vector3>(2048);

		private void DrawAxisLabels(Rect rect)
		{
			var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperLeft };
			GUI.Label(new Rect(rect.xMin + 4, rect.yMin + 4, 200, 16), $"X: speed km/h ({_minSpeed * KmhPerMs:0.##} .. {_maxSpeed * KmhPerMs:0.##})", style);
			style.alignment = TextAnchor.UpperRight;
			GUI.Label(new Rect(rect.xMax - 240, rect.yMin + 4, 236, 16), $"Y: accel m/s^2 ({_yMin:0.##} .. {_yMax:0.##})", style);
		}

		private void DrawCursorReadout(Rect rect, List<CurveData> curves, float maxRpm, EngineService engine)
		{
			var e = Event.current;
			if (e == null) return;
			var mp = e.mousePosition;
			if (!rect.Contains(mp)) return;

			float nx = Mathf.InverseLerp(rect.xMin, rect.xMax, mp.x);
			float speed = Mathf.Lerp(_minSpeed, _maxSpeed, nx);

			Handles.BeginGUI();
			Handles.color = EditorGUIUtility.isProSkin ? new Color(1,1,1,0.25f) : new Color(0,0,0,0.35f);
			Handles.DrawLine(new Vector3(mp.x, rect.yMin), new Vector3(mp.x, rect.yMax));
			Handles.EndGUI();

			var box = new Rect(mp.x + 10, Mathf.Clamp(mp.y - 10, rect.yMin, rect.yMax - 140),
				240, _plotMode == PlotMode.SelectedGear ? 74 : Mathf.Min(240, 28 + 18 * curves.Count + 12));
			EditorGUI.DrawRect(box, EditorGUIUtility.isProSkin ? new Color(0,0,0,0.6f) : new Color(1,1,1,0.9f));
			var inner = new Rect(box.x + 6, box.y + 4, box.width - 12, box.height - 8);

			var labelStyle = new GUIStyle(EditorStyles.miniLabel);
			labelStyle.richText = true;

			float y = inner.y;
			GUI.Label(new Rect(inner.x, y, inner.width, 16), $"<b>Speed:</b> {speed * KmhPerMs:0.###} km/h", labelStyle); y += 18f;

			if (_plotMode == PlotMode.SelectedGear)
			{
				int gear = _transmissionSO != null && _transmissionSO.Gears != null && _transmissionSO.Gears.Length > 0
					? Mathf.Clamp(_selectedGearIndex, 0, _transmissionSO.Gears.Length - 1)
					: 0;

				GUI.Label(new Rect(inner.x, y, inner.width, 16), $"<b>Accel:</b> {EvaluateAccelInGear(engine, gear, speed):0.###} m/s^2"); y += 18f;
				GUI.Label(new Rect(inner.x, y, inner.width, 16), $"<b>RPM:</b> {GetRpmForSpeed(speed, gear):0}  (max {maxRpm:0})");
			}
			else
			{
				foreach (var c in curves)
				{
					GUI.Label(new Rect(inner.x, y, inner.width, 16),
						$"G{c.GearIndex}: a={GetAccelForGear(engine, c.GearIndex, speed):0.###}, rpm={GetRpmForSpeed(speed, c.GearIndex):0}");
					y += 18f;
				}
			}
		}

		private float GetAccelForGear(EngineService engine, int gearIndex, float speed)
		{
			return EvaluateAccelInGear(engine, gearIndex, speed);
		}

		private void DrawLegend(Rect rect, List<CurveData> curves)
		{
			if (curves == null || curves.Count == 0) return;
			float boxW = 150f;
			float boxH = 18f + curves.Count * 18f;
			var legendRect = new Rect(rect.xMax - boxW - 8, rect.yMax - boxH - 8, boxW, boxH);
			EditorGUI.DrawRect(legendRect, EditorGUIUtility.isProSkin ? new Color(0,0,0,0.4f) : new Color(1,1,1,0.75f));
			legendRect = new Rect(legendRect.x + 6, legendRect.y + 4, legendRect.width - 12, legendRect.height - 8);

			var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.UpperLeft };
			GUI.Label(new Rect(legendRect.x, legendRect.y, legendRect.width, 16), "Legend:", labelStyle);
			float y = legendRect.y + 18f;
			foreach (var c in curves)
			{
				var swatch = new Rect(legendRect.x, y + 2, 14, 14);
				EditorGUI.DrawRect(swatch, c.Color);
				GUI.Label(new Rect(legendRect.x + 18, y, legendRect.width - 20, 16), c.Label, EditorStyles.miniLabel);
				y += 18f;
			}
		}
	}
}
#endif
