#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Car.Gears.Editor
{
	public class AccelerationGraphWindow : EditorWindow
	{
		private const int DefaultSamples = 512;

		private GearDataRpm _gearData;

		private enum PlotMode { SelectedGear, AllGears }
		private PlotMode _plotMode = PlotMode.SelectedGear;

		private int _selectedGearIndex = 0;
		private float _minSpeed = 0f;
		private float _maxSpeed = 50f;
		private int _samples = DefaultSamples;

		private bool _autoY = true;
		private float _yMin = -5f;
		private float _yMax = 50f;

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

		private void OnGUI()
		{
			EditorGUILayout.Space(4);
			EditorGUI.BeginChangeCheck();
			_gearData = (GearDataRpm)EditorGUILayout.ObjectField(new GUIContent("GearDataRpm", "Выберите ScriptableObject c настройками передач"), _gearData, typeof(GearDataRpm), false);
			if (EditorGUI.EndChangeCheck())
			{
				ResetRangesToData();
			}

			if (_gearData == null)
			{
				EditorGUILayout.HelpBox("Укажите GearDataRpm, чтобы увидеть график.", MessageType.Info);
				return;
			}

			DrawControls();
			EditorGUILayout.Space(6);

			var curves = BuildCurves();
			if (curves.Count == 0)
				return;

			if (_autoY)
			{
				float minA = float.PositiveInfinity;
				float maxA = float.NegativeInfinity;
				foreach (var c in curves)
				{
					minA = Mathf.Min(minA, c.MinY);
					maxA = Mathf.Max(maxA, c.MaxY);
				}

				float pad = Mathf.Max(0.01f * Mathf.Max(1f, Mathf.Abs(maxA - minA)), 0.1f);
				_yMin = minA - pad;
				_yMax = maxA + pad;

				if (Mathf.Approximately(_yMin, _yMax))
				{
					_yMin -= 1f; _yMax += 1f;
				}
			}

			Rect rect = GUILayoutUtility.GetRect(position.width - 16f, Mathf.Max(220f, position.height - 220f));
			rect.x += 8f; rect.width -= 16f;

			DrawGraphBackground(rect);
			//DrawGrid(rect, 10, 6);
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
				DrawCursorReadout(rect, curves);

			Repaint();
		}

		private void DrawControls()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				_plotMode = (PlotMode)GUILayout.Toolbar((int)_plotMode, new[] {"Selected gear", "All gears"}, GUILayout.Height(22));

				if (GUILayout.Button(new GUIContent("Fit X", "Подогнать диапазон скорости под выбранную передачу"), GUILayout.Width(70)))
					FitXToSelected();
				if (GUILayout.Button(new GUIContent("Fit Y", "Авто‑подбор вертикального диапазона"), GUILayout.Width(70)))
					_autoY = true;
			}

			using (new EditorGUILayout.VerticalScope("box"))
			{
				if (_plotMode == PlotMode.SelectedGear)
				{
					_selectedGearIndex = Mathf.Clamp(EditorGUILayout.IntSlider(new GUIContent("Gear Index"), _selectedGearIndex, 0, Mathf.Max(0, _gearData.GearsCount - 1)), 0, Mathf.Max(0, _gearData.GearsCount - 1));
				}
				else
				{
					EditorGUILayout.LabelField("Gears:", $"0..{Mathf.Max(0, _gearData.GearsCount - 1)}");
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					_minSpeed = EditorGUILayout.FloatField(new GUIContent("Speed Min"), _minSpeed);
					_maxSpeed = EditorGUILayout.FloatField(new GUIContent("Speed Max"), _maxSpeed);
				}

				_samples = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Samples"), _samples), 16, 8192);

				using (new EditorGUILayout.HorizontalScope())
				{
					_autoY = EditorGUILayout.Toggle(new GUIContent("Auto Y"), _autoY);
					EditorGUI.BeginDisabledGroup(_autoY);
					_yMin = EditorGUILayout.FloatField(new GUIContent("Y Min"), _yMin);
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
			if (_gearData != null && _gearData.GearsCount > 0)
			{
				var mx = ComputeMaxSpeedForGear(_gearData, _selectedGearIndex);
				_minSpeed = 0f;
				_maxSpeed = Mathf.Max(5f, mx * 1.2f);
			}
			else
			{
				_minSpeed = 0f; _maxSpeed = 50f;
			}
			_autoY = true;
		}

		private void FitXToSelected()
		{
			if (_gearData == null || _gearData.GearsCount == 0) return;
			if (_plotMode == PlotMode.SelectedGear)
			{
				var mx = ComputeMaxSpeedForGear(_gearData, Mathf.Clamp(_selectedGearIndex, 0, _gearData.GearsCount - 1));
				_minSpeed = 0f;
				_maxSpeed = Mathf.Max(5f, mx * 1.2f);
			}
			else
			{
				float maxAcross = 0f;
				for (int i = 0; i < _gearData.GearsCount; i++)
					maxAcross = Mathf.Max(maxAcross, ComputeMaxSpeedForGear(_gearData, i));
				_minSpeed = 0f;
				_maxSpeed = Mathf.Max(5f, maxAcross * 1.2f);
			}
		}

		private struct CurveData
		{
			public List<Vector3> Points;
			public float MinY, MaxY;
			public Color Color;
			public string Label;
			public int GearIndex;
		}

		private List<CurveData> BuildCurves()
		{
			var list = new List<CurveData>();
			if (_gearData == null || _gearData.GearsCount == 0) return list;

			int toDraw = _plotMode == PlotMode.SelectedGear ? 1 : _gearData.GearsCount;

			for (int gi = 0; gi < toDraw; gi++)
			{
				int gearIdx = _plotMode == PlotMode.SelectedGear ? Mathf.Clamp(_selectedGearIndex, 0, _gearData.GearsCount - 1) : gi;

				var svc = new TransmissionService(_gearData);
				for (int s = 0; s < gearIdx; s++) svc.ShiftUpSafe();

				var pts = new List<Vector3>(_samples);
				float minA = float.PositiveInfinity;
				float maxA = float.NegativeInfinity;

				if (Mathf.Approximately(_maxSpeed, _minSpeed))
					_maxSpeed = _minSpeed + 1f;

				for (int i = 0; i < _samples; i++)
				{
					float t = (float)i / (_samples - 1);
					float spd = Mathf.Lerp(_minSpeed, _maxSpeed, t);
					float a = svc.GetAcceleration(spd);
					minA = Mathf.Min(minA, a);
					maxA = Mathf.Max(maxA, a);
					pts.Add(new Vector3(spd, a, 0f));
				}

				Color col = _plotMode == PlotMode.SelectedGear
					? EditorGUIUtility.isProSkin ? new Color(0.5f, 0.9f, 1f, 1f) : new Color(0.1f, 0.4f, 0.8f, 1f)
					: Color.HSVToRGB(Mathf.Repeat(gearIdx * 0.13f, 1f), 0.8f, 0.95f);

				list.Add(new CurveData
				{
					Points = pts,
					MinY = minA,
					MaxY = maxA,
					Color = col,
					Label = $"Gear {gearIdx}",
					GearIndex = gearIdx
				});
			}

			return list;
		}

		private float ComputeMaxSpeedForGear(GearDataRpm data, int gearIndex)
		{
			gearIndex = Mathf.Clamp(gearIndex, 0, Mathf.Max(0, data.GearsCount - 1));
			var g = data.GetGear(gearIndex);
			float denom = Mathf.Max(0.0001f, g.GearRatio * data.SpeedToRpmFactor);
			return Mathf.Max(0f, data.RpmRedline / denom - data.SpeedShift);
		}

		private float GetRpmForSpeed(GearDataRpm data, int gearIndex, float speed)
		{
			gearIndex = Mathf.Clamp(gearIndex, 0, Mathf.Max(0, data.GearsCount - 1));
			var g = data.GetGear(gearIndex);
			float rpm = (speed + data.SpeedShift) * g.GearRatio * data.SpeedToRpmFactor;
			return Mathf.Max(0f, rpm);
		}

		private void DrawGraphBackground(Rect rect)
		{
			EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(0.95f, 0.95f, 0.95f, 1f));
		}

		private void DrawGrid(Rect rect, int vLines, int hLines)
		{
			Handles.BeginGUI();
			Handles.color = EditorGUIUtility.isProSkin ? new Color(1,1,1,0.06f) : new Color(0,0,0,0.06f);

			for (int i = 1; i < vLines; i++)
			{
				float t = i/(float)vLines;
				float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
				Handles.DrawLine(new Vector2(x, rect.yMin), new Vector2(x, rect.yMax));
			}
			for (int j = 1; j < hLines; j++)
			{
				float t = j/(float)hLines;
				float y = Mathf.Lerp(rect.yMin, rect.yMax, t);
				Handles.DrawLine(new Vector2(rect.xMin, y), new Vector2(rect.xMax, y));
			}
			Handles.EndGUI();
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
			GUI.Label(new Rect(rect.xMin + 4, rect.yMin + 4, 200, 16), $"X: speed ({_minSpeed:0.##} .. {_maxSpeed:0.##})", style);
			style.alignment = TextAnchor.UpperRight;
			GUI.Label(new Rect(rect.xMax - 200, rect.yMin + 4, 196, 16), $"Y: acceleration ({_yMin:0.##} .. {_yMax:0.##})", style);
		}

		private void DrawCursorReadout(Rect rect, List<CurveData> curves)
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

			var box = new Rect(mp.x + 10, Mathf.Clamp(mp.y - 10, rect.yMin, rect.yMax - 140), 240, _plotMode == PlotMode.SelectedGear ? 74 : Mathf.Min(240, 28 + 18 * curves.Count + 12));
			EditorGUI.DrawRect(box, EditorGUIUtility.isProSkin ? new Color(0,0,0,0.6f) : new Color(1,1,1,0.9f));
			var inner = new Rect(box.x + 6, box.y + 4, box.width - 12, box.height - 8);

			var labelStyle = new GUIStyle(EditorStyles.miniLabel);
			labelStyle.richText = true;

			float y = inner.y;
			GUI.Label(new Rect(inner.x, y, inner.width, 16), $"<b>Speed:</b> {speed:0.###}", labelStyle); y += 18f;

			if (_plotMode == PlotMode.SelectedGear)
			{
				int gi = Mathf.Clamp(_selectedGearIndex, 0, _gearData.GearsCount - 1);
				var svc = new TransmissionService(_gearData);
				for (int s = 0; s < gi; s++) svc.ShiftUpSafe();
				float accel = svc.GetAcceleration(speed);
				float rpm = GetRpmForSpeed(_gearData, gi, speed);
				GUI.Label(new Rect(inner.x, y, inner.width, 16), $"<b>Accel:</b> {accel:0.###}"); y += 18f;
				GUI.Label(new Rect(inner.x, y, inner.width, 16), $"<b>RPM:</b> {rpm:0}  (idle {_gearData.RpmIdle:0}, redline {_gearData.RpmRedline:0})");
			}
			else if (_plotMode == PlotMode.AllGears)
			{
				foreach (var c in curves)
				{
					int gi = c.GearIndex;
					var svc = new TransmissionService(_gearData);
					for (int s = 0; s < gi; s++) svc.ShiftUpSafe();
					float accel = svc.GetAcceleration(speed);
					float rpm = GetRpmForSpeed(_gearData, gi, speed);
					GUI.Label(new Rect(inner.x, y, inner.width, 16), $"G{gi}: a={accel:0.###}, rpm={rpm:0}");
					y += 18f;
				}
			}
		}

		private void DrawLegend(Rect rect, List<CurveData> curves)
		{
			if (curves == null || curves.Count == 0) return;
			float boxW = 140f;
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
