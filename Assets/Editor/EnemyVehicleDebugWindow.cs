using System;
using System.Text;
using Game.Enemies.Vehicle;
using UnityEditor;
using UnityEngine;

namespace Game.Enemies.Editor
{
    /// <summary>
    /// Окно отладки EnemyVehicleController: показывает всю статистику в реальном времени.
    /// Открыть: Window → Enemies → Enemy Vehicle Debug
    /// </summary>
    public sealed class EnemyVehicleDebugWindow : EditorWindow
    {
        private EnemyVehicleController _target;
        private Vector2 _scroll;
        private bool _autoSelect = true;
        private float _updateInterval = 0.1f;
        private double _lastUpdate;

        [MenuItem("Window/Enemies/Enemy Vehicle Debug", priority = 1000)]
        public static void Open() => GetWindow<EnemyVehicleDebugWindow>("Enemy Debug");

        private void OnEnable()
        {
            titleContent = new GUIContent("Enemy Debug", EditorGUIUtility.IconContent("d_PreMatQuad").image);
            EditorApplication.update += Repaint;
        }

        private void OnDisable() => EditorApplication.update -= Repaint;

        private void OnGUI()
        {
            // Header: target selection
            DrawTargetSelector();
            EditorGUILayout.Space(4);

            if (_target == null)
            {
                EditorGUILayout.HelpBox("Выбери врага с EnemyVehicleController в Hierarchy/Scene.", MessageType.Info);
                return;
            }

            // Throttle updates in Edit mode
            if (!Application.isPlaying && EditorApplication.timeSinceStartup - _lastUpdate < _updateInterval)
            {
                DrawCached();
                return;
            }
            _lastUpdate = EditorApplication.timeSinceStartup;

            DrawAllStats();
        }

        private void DrawTargetSelector()
        {
            EditorGUILayout.BeginHorizontal();
            _autoSelect = EditorGUILayout.ToggleLeft("Auto-select from selection", _autoSelect, GUILayout.Width(180));
            EditorGUILayout.LabelField("Target:", GUILayout.Width(50));
            _target = (EnemyVehicleController)EditorGUILayout.ObjectField(_target, typeof(EnemyVehicleController), true);
            EditorGUILayout.EndHorizontal();

            if (_autoSelect && Selection.activeGameObject != null)
            {
                var sel = Selection.activeGameObject.GetComponent<EnemyVehicleController>();
                if (sel != null) _target = sel;
            }
        }

        private void DrawAllStats()
        {
            var m = _target.Model;
            var rb = _target.GetComponent<Rigidbody>();
            var cfg = m.Config;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // --- Road / Tracking ---
            DrawSection("📍 Road & Tracking", () =>
            {
                EditorGUILayout.LabelField("Has Road", m.HasRoad ? "✅ Yes" : "❌ No");
                if (!m.HasRoad) return;

				//DrawRow("Current Path", (m.CurrentPath?.name ?? "null");
                DrawRow("Distance Along Path", $"{m.DistanceAlongPath:F2} / {m.CurrentPath?.Length:F2} m");
                DrawRow("Current Lane", m.CurrentLane.ToString());
                DrawRow("Lateral Offset", $"{m.LateralOffset:F2} m");
                DrawRow("Target Lateral Offset", $"{m.TargetLateralOffset:F2} m");
                DrawRow("Road Curvature", $"{m.Curvature:F4} 1/m");
                //DrawRow("Last Valid Path", m.LastValidPath?.name ?? "null");
                DrawRow("Last Valid S", $"{m.LastValidDistance:F2} m");
            });

            // --- Pure Pursuit ---
            DrawSection("🎯 Pure Pursuit", () =>
            {
                if (!m.HasRoad) return;

                var cfgS = cfg.Steering;
                float speed = Mathf.Max(0f, m.ForwardSpeed);
                float baseL = Mathf.Clamp(speed * cfgS.LookaheadTime + cfgS.MinLookahead, cfgS.MinLookahead, cfgS.MaxLookahead);
                float denom = 1f + cfgS.CurvatureGain * Mathf.Abs(m.Curvature) * baseL;
                float lookahead = Mathf.Clamp(baseL / denom, cfgS.MinLookahead * 0.7f, cfgS.MaxLookahead);

                DrawRow("Forward Speed", $"{speed:F2} m/s");
                DrawRow("Base Lookahead", $"{baseL:F2} m");
                DrawRow("Curvature-Adjusted Lookahead", $"{lookahead:F2} m");
                DrawRow("Min / Max Lookahead", $"{cfgS.MinLookahead} / {cfgS.MaxLookahead} m");
                DrawRow("Lookahead Time", $"{cfgS.LookaheadTime} s");
                DrawRow("Curvature Gain", $"{cfgS.CurvatureGain}");
                DrawRow("Wheelbase", $"{cfgS.Wheelbase} m");
                DrawRow("Max Steer Angle", $"{cfgS.MaxSteerAngleDeg}°");
                DrawRow("Last Goal World", m.LastGoalWorld.ToString("F2"));
                DrawRow("Last Steer Angle", $"{m.LastSteerAngleRad * Mathf.Rad2Deg:F2}°");
                DrawRow("Target Lateral Offset", $"{m.TargetLateralOffset:F2} m");
            });

            // --- Physics / Yaw ---
            DrawSection("⚙️ Physics & Yaw", () =>
            {
                if (rb != null)
                {
                    var localVel = Quaternion.Inverse(rb.rotation) * rb.linearVelocity;
                    var localAng = Quaternion.Inverse(rb.rotation) * rb.angularVelocity;
                    DrawRow("Rb Velocity (world)", rb.linearVelocity.ToString("F2"));
                    DrawRow("Rb Velocity (local)", $"Fwd: {localVel.z:F2}, Lat: {localVel.x:F2}, Up: {localVel.y:F2}");
                    DrawRow("Rb Angular Vel (local)", $"Yaw: {localAng.y * Mathf.Rad2Deg:F2}°/s");
                    DrawRow("Rb Mass", $"{rb.mass} kg");
                    DrawRow("Center Of Mass", rb.centerOfMass.ToString("F2"));
                }
                var yaw = cfg.Yaw;
                DrawRow("Yaw Kp / Kd", $"{yaw.Kp} / {yaw.Kd}");
                DrawRow("Yaw Derivative Tau", $"{yaw.DerivativeTau} s");
                DrawRow("Max Yaw Accel", $"{yaw.MaxAccel} rad/s²");
                DrawRow("Prev Yaw Rate", $"{m.PrevYawRate:F4} rad/s");
                DrawRow("Filtered Yaw Alpha", $"{m.FilteredYawAlpha:F4}");
                DrawRow("Yaw Controller Init", m.YawControllerInitialized ? "✅" : "❌");
            });

            // --- Speed / Drivetrain ---
            DrawSection("🚗 Speed & Drivetrain", () =>
            {
                var sp = cfg.Speed;
                var dt = cfg.Drivetrain;
                DrawRow("Target Speed", $"{m.TargetSpeed:F2} m/s");
                DrawRow("Forward Speed", $"{m.ForwardSpeed:F2} m/s");
                DrawRow("Speed Kp", $"{sp.Kp}");
                DrawRow("Max Drive Accel", $"{sp.MaxDriveAccel} m/s²");
                DrawRow("Max Brake Accel", $"{sp.MaxBrakeAccel} m/s²");
                DrawRow("Corner Friction μ", $"{sp.CornerFrictionMu}");
                DrawRow("Current Gear", m.CurrentGear.ToString());
                DrawRow("Current RPM", $"{m.CurrentRpm:F0}");
                DrawRow("Normalized RPM", $"{m.NormalizedRpm:F2}");
                DrawRow("Engine Load", $"{m.EngineLoad:F2}");
                DrawRow("Shift Up / Down RPM", $"{dt.ShiftUpRpm} / {dt.ShiftDownRpm}");
                DrawRow("Idle / Max RPM", $"{dt.IdleRpm} / {dt.MaxRpm}");
                DrawRow("Gear Ratios", string.Join(", ", dt.GearRatios));
            });

            // --- Stability ---
            DrawSection("🛡️ Stability", () =>
            {
                var st = cfg.Stability;
                DrawRow("Lateral Damping", $"{st.LateralDamping}");
                DrawRow("High-Speed Damp Mult", $"{st.HighSpeedDampingMultiplier}");
                DrawRow("Upright Stiffness", $"{st.UprightStiffness}");
                DrawRow("Upright Damping", $"{st.UprightDamping}");
                DrawRow("Max Upright Accel", $"{st.MaxUprightAccel}");
                DrawRow("Lateral Slip (rad)", $"{m.LateralSlipRadians:F4}");
                DrawRow("Stuck Timer", $"{m.StuckTimer:F2} s");
                DrawRow("Stuck Threshold", $"{cfg.Body.StuckSpeedThreshold} m/s for {cfg.Body.StuckDuration} s");
            });

            // --- Spatial / Brain ---
            DrawSection("🧠 Brain & Spatial", () =>
            {
                DrawRow("Spatial Agent ID", m.SpatialAgentId >= 0 ? m.SpatialAgentId.ToString() : "Not registered");
                DrawRow("Has Brain", _target.GetType().GetField("_brain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_target) != null ? "✅" : "❌");
                DrawRow("Has Command Modifier", _target.GetType().GetField("_commandModifier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_target) != null ? "✅" : "❌");
            });

            EditorGUILayout.EndScrollView();
        }

        private void DrawSection(string title, Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            content();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(200));
            EditorGUILayout.SelectableLabel(value, EditorStyles.label, GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCached()
        {
            EditorGUILayout.LabelField("(Throttled in Edit mode — Enter Play Mode for live updates)", EditorStyles.miniLabel);
        }
    }
}