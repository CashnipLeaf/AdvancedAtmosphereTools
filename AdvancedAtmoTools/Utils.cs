using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AdvancedAtmosphereTools
{
    internal static class Utils
    {
        internal const string version = "1.0.0";
        internal static string GameDataPath => KSPUtil.ApplicationRootPath + "GameData/";
        internal static Dictionary<string, string> LOCCache; //localization cache

        internal static void LogInfo(string message) => Debug.Log("[AdvAtmoTools] " + message); //General information
        internal static void LogWarning(string message) => Debug.LogWarning("[AdvAtmoTools][WARNING] " + message); //Warnings indicate that AdvAtmoTools may be operating at reduced functionality.
        internal static void LogAPI(string message) => Debug.Log("[AdvAtmoTools][API] " + message); //API Logging
        internal static void LogAPIWarning(string message) => Debug.LogWarning("[AdvAtmoTools][API WARNING] " + message); //API warnings
        internal static void LogError(string message) => Debug.LogError("[AdvAtmoTools][ERROR] " + message); //Errors that invoke fail-safe protections.

        //------------------------------MATH AND RELATED-------------------------
        internal static double Epsilon => float.Epsilon * 16d; //value that is very nearly zero to prevent the log interpolation from breaking

        internal static float BiLerp(float first1, float second1, float first2, float second2, float by1, float by2)
        {
            return Mathf.Lerp(Mathf.Lerp(first1, second1, by1), Mathf.Lerp(first2, second2, by1), by2);
        }

        //allow for altitude spacing based on some factor
        internal static double ScaleAltitude(double nX, double xBase, int upperbound, out int int1, out int int2)
        {
            nX = UtilMath.Clamp01(nX);
            double z = (xBase <= 1.0 ? nX : ((Math.Pow(xBase, -nX * upperbound) - 1) / (Math.Pow(xBase, -1 * upperbound) - 1))) * upperbound;
            int1 = Clamp((int)Math.Floor(z), 0, upperbound); //layer 1
            int2 = Clamp(int1 + 1, 0, upperbound); //layer 2
            return nX >= 1d ? 1d : UtilMath.Clamp01(z - Math.Truncate(z));
        }

        internal static double InterpolatePressure(double first, double second, double by)
        {
            if (first < 0.0 || second < 0.0) //negative values will break the logarithm, so they are not allowed.
            {
                throw new ArgumentOutOfRangeException();
            }
            if (first <= Epsilon || second <= Epsilon)
            {
                return UtilMath.Lerp(first, second, by);
            }
            double scalefactor = Math.Log(first / second);
            if (double.IsNaN(scalefactor))
            {
                throw new NotFiniteNumberException();
            }
            return first * Math.Pow(Math.E, -1 * UtilMath.Lerp(0.0, UtilMath.Clamp(scalefactor, float.MinValue, float.MaxValue), by));
        }

        //Apparently no such function exists for integers in either UtilMath or Mathf. Why?
        internal static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);


        //--------------------EXTENSION METHODS---------------------

        //faster extension methods for Vector3
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Add(ref this Vector3 v, Vector3 other)
        {
            v.x += other.x;
            v.y += other.y;
            v.z += other.z;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Subtract(ref this Vector3 v, Vector3 other)
        {
            v.x -= other.x;
            v.y -= other.y;
            v.z -= other.z;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Set(ref this Vector3 v, Vector3 other)
        {
            v.x = other.x;
            v.y = other.y;
            v.z = other.z;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void MultiplyByConstant(ref this Vector3 v, float other)
        {
            v.x *= other;
            v.y *= other;
            v.z *= other;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LerpWith(ref this Vector3 v, Vector3 other, float by)
        {
            by = Mathf.Clamp01(by);
            v.x = (v.x * (1.0f - by)) + (other.x * by);
            v.y = (v.x * (1.0f - by)) + (other.y * by);
            v.z = (v.y * (1.0f - by)) + (other.z * by);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Multiply(ref this Vector3 v, Vector3 other)
        {
            v.x *= other.x;
            v.y *= other.y;
            v.z *= other.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Zero(ref this Vector3 v) => v.x = v.y = v.z = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFinite(ref this Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsZero(ref this Vector3 v) => v.x == 0.0f && v.y == 0.0f && v.z == 0.0f;
    }

    //struct to encapsulate data info nicely
    public struct DataInfo
    {
        public int x1;
        public int x2;
        public double xlerp;
        public int y1;
        public int y2;
        public double ylerp;
        public int z1;
        public int z2;
        public double zlerp;
        public int t1;
        public int t2;
        public double tlerp;
        public bool abovetop;

        public DataInfo(int x1, int x2, double xlerp, int y1, int y2, double ylerp, int z1, int z2, double zlerp, int t1, int t2, double tlerp, bool abovetop)
        {
            this.x1 = x1;
            this.x2 = x2;
            this.xlerp = xlerp;
            this.y1 = y1;
            this.y2 = y2;
            this.ylerp = ylerp;
            this.z1 = z1;
            this.z2 = z2;
            this.zlerp = zlerp;
            this.t1 = t1;
            this.t2 = t2;
            this.tlerp = tlerp;
            this.abovetop = abovetop;
        }

        public static DataInfo Zero => new DataInfo(0, 0, 0.0, 0, 0, 0.0, 0, 0, 0.0, 0, 0, 0.0, false);

        public void SetNew(DataInfo data)
        {
            x1 = data.x1;
            x2 = data.x2;
            xlerp = data.xlerp;
            y1 = data.y1;
            y2 = data.y2;
            ylerp = data.ylerp;
            z1 = data.z1;
            z2 = data.z2;
            zlerp = data.zlerp;
            t1 = data.t1;
            t2 = data.t2;
            tlerp = data.tlerp;
            abovetop = data.abovetop;
        }

        public void SetNew(int x1, int x2, double xlerp, int y1, int y2, double ylerp, int z1, int z2, double zlerp, int t1, int t2, double tlerp, bool abovetop)
        {
            this.x1 = x1;
            this.x2 = x2;
            this.xlerp = xlerp;
            this.y1 = y1;
            this.y2 = y2;
            this.ylerp = ylerp;
            this.z1 = z1;
            this.z2 = z2;
            this.zlerp = zlerp;
            this.t1 = t1;
            this.t2 = t2;
            this.tlerp = tlerp;
            this.abovetop = abovetop;
        }

        public void SetZero()
        {
            x1 = x2 = y1 = y2 = z1 = z2 = t1 = t2 = 0;
            xlerp = ylerp = zlerp = tlerp = 0;
            abovetop = false;
        }
    }
}
