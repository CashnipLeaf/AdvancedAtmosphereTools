using System;

namespace MCWS_ExoPlaSimReader
{
    internal class ExoPlaSim_BodyData
    {
        private string body;

        private float[][,,] WindDataX;
        private float[][,,] WindDataY;
        private float[][,,] WindDataZ;
        internal float WindScaleFactor = float.NaN;
        internal double WindTimeStep = double.NaN;
        internal bool HasWind => WindDataX != null && WindDataY != null && WindDataZ != null && !double.IsNaN(WindTimeStep) && !float.IsNaN(WindScaleFactor);

        private float[][,,] TemperatureData;
        internal float TemperatureScaleFactor = float.NaN;
        internal double TemperatureTimeStep = double.NaN;
        internal bool HasTemperature => TemperatureData != null && !double.IsNaN(TemperatureTimeStep) && !float.IsNaN(TemperatureScaleFactor);

        private float[][,,] PressureData;
        internal float PressureScaleFactor = float.NaN;
        internal double PressureTimeStep = double.NaN;
        internal bool HasPressure => PressureData != null && !double.IsNaN(PressureTimeStep) && !float.IsNaN(PressureScaleFactor);

        internal ExoPlaSim_BodyData(string body)
        {
            this.body = body;
        }

        internal void AddWindData(float[][,,] WindX, float[][,,] WindY, float[][,,] WindZ, float scalefactor, double timestep)
        {
            WindDataX = WindX;
            WindDataY = WindY;
            WindDataZ = WindZ;
            WindScaleFactor = scalefactor;
            WindTimeStep = timestep;
        }

        internal void AddTemperatureData(float[][,,] Temp, float scalefactor, double timestep)
        {
            TemperatureData = Temp;
            TemperatureScaleFactor = scalefactor;
            TemperatureTimeStep = timestep;
        }

        internal void AddPressureData(float[][,,] Press, float scalefactor, double timestep)
        {
            PressureData = Press;
            PressureScaleFactor = scalefactor;
            PressureTimeStep = timestep;
        }

        internal float[,,] GetWindX(double time) => HasWind ? WindDataX[(int)Math.Floor(time / WindTimeStep) % WindDataX.Length] : null;
        internal float[,,] GetWindY(double time) => HasWind ? WindDataY[(int)Math.Floor(time / WindTimeStep) % WindDataY.Length] : null;
        internal float[,,] GetWindZ(double time) => HasWind ? WindDataZ[(int)Math.Floor(time / WindTimeStep) % WindDataZ.Length] : null;
        internal float[,,] GetTemperature(double time) => HasTemperature ? TemperatureData[(int)Math.Floor(time / TemperatureTimeStep) % TemperatureData.Length] : null;
        internal float[,,] GetPressure(double time) => HasPressure ? PressureData[(int)Math.Floor(time / PressureTimeStep) % PressureData.Length] : null;
    }
}
