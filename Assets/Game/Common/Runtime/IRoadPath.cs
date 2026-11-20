using Dreamteck.Splines;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Common.Runtime
{
    public interface IRoadPath
    {
        /// <summary>Сэмпл сплайна по дистанции от входа сегмента.</summary>
        SplineSample SampleAtDistance(float distance, Spline.Direction dir = Spline.Direction.Forward);
        
        /// <summary>Длина сплайна сегмента в метрах.</summary>
        float Length { get; }
        
        /// <summary>Ширина дороги в точке: (левая от центра, правая от центра) в метрах.</summary>
        (float left, float right) GetRoadWidthAt(float distance);
        
        /// <summary>Латеральный оффсет центра полосы относительно центра сплайна (метры).</summary>
        float GetLaneOffset(int laneIndex);
        
        /// <summary>Количество полос на этом сегменте.</summary>
        int LaneCount { get; }
        
        /// <summary>Следующие сегменты для данной полосы (0..N для развилок).</summary>
        IEnumerable<IRoadPath> GetNextPaths(int laneIndex);
        
        /// <summary>Предыдущий сегмент (для задней навигации).</summary>
        IRoadPath PreviousPath { get; }
        
        /// <summary>Ближайшая точка на сплайне к мировой позиции.</summary>
        SplineSample GetClosestSample(Vector3 worldPosition);
        
        /// <summary>Индекс полосы под мировой позицией (проекция на сплайн → ближайшая полоса).</summary>
        int GetLaneAtPosition(Vector3 worldPosition);
        
        /// <summary>Существует ли полоса на заданной дистанции (учитывает сужения/расширения).</summary>
        bool IsLaneActiveAt(int laneIndex, float distance);
    }
}