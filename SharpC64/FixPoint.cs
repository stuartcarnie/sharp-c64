using System;
using System.Collections.Generic;
using System.Text;

namespace SharpC64
{
    public class FixPoint
    {
        int x;
        /*
         *  FixPoint.i
         *
         *  Provides fixpoint arithmetic (for use in SID.cpp)
         *  You need to define FIXPOINT_PREC (number of fractional bits) and
         *  ldSINTAB (ld of the size of the sinus table) as well M_PI
         *  _before_ including this file.
         *  Requires at least 32bit ints!
         *  (C) 1997 Andreas Dehmel
         */


        const int FIXPOINT_BITS = 32;
        // Sign-bit
        const int FIXPOINT_SIGN = (1 << (FIXPOINT_BITS - 1));

        const int ldSINTAB = 9;

#if FIXPOINT_PREC_SMALL
        const int FIXPOINT_PREC = 16;
#else
        const int FIXPOINT_PREC = 32;
#endif

        #region static functions
        /*
 *  Elementary functions for the FixPoint class
 */

        // Multiplies two fixpoint numbers, result is a fixpoint number.
        static int fixmult(int x, int y)
        {
            UInt32 a, b;
            bool sign;

            sign = (x ^ y) < 0;
            if (x < 0) { x = -x; }
            if (y < 0) { y = -y; }
            // a, b : integer part; x, y : fractional part. All unsigned now (for shift right)!!!
            a = (((UInt32)x) >> FIXPOINT_PREC); x &= (int)~(a << FIXPOINT_PREC);
            b = (((UInt32)y) >> FIXPOINT_PREC); y &= (int)~(b << FIXPOINT_PREC);
            x = (int)(((a * b) << FIXPOINT_PREC) + (a * y + b * x) +
                ((UInt32)((x * y) + (1 << (FIXPOINT_PREC - 1))) >> FIXPOINT_PREC));
#if FIXPOINT_SIGN
  if (x < 0) {x ^= FIXPOINT_SIGN;}
#endif
            if (sign) { x = -x; }
            return (x);
        }


        // Multiplies a fixpoint number with an integer, result is a 32 bit (!) integer in
        // contrast to using the standard member-functions which can provide only (32-FIXPOINT_PREC)
        // valid bits.
        static int intmult(int x, int y)	// x is fixpoint, y integer
        {
            UInt32 i, j;
            bool sign;

            sign = (x ^ y) < 0;
            if (x < 0) { x = -x; }
            if (y < 0) { y = -y; }
            i = (((UInt32)x) >> 16); x &= (int)~(i << 16);	// split both into 16.16 parts
            j = (((UInt32)y) >> 16); y &= (int)~(j << 16);
#if FIXPOINT_PREC_SMALL
  // This '32' is independent of the number of bits used, it's due to the 16 bit shift
  i = ((i*j) << (32 - FIXPOINT_PREC)) + ((i*y + j*x) << (16 - FIXPOINT_PREC)) +
      ((UInt32)(x*y + (1 << (FIXPOINT_PREC - 1))) >> FIXPOINT_PREC);
#else
            {
                UInt32 h;

                h = (UInt32)(i * y + j * x);
                i = ((i * j) << (32 - FIXPOINT_PREC)) + (h >> (FIXPOINT_PREC - 16));
                h &= ((1 << (FIXPOINT_PREC - 16)) - 1); x *= y;
                i += (UInt32)(x >> FIXPOINT_PREC); x &= ((1 << FIXPOINT_PREC) - 1);
                i += (UInt32)(((h + (x >> 16)) + (1 << (FIXPOINT_PREC - 17))) >> (FIXPOINT_PREC - 16));
            }
#endif

#if FIXPOINT_SIGN
  if (i < 0) {i ^= FIXPOINT_SIGN;}
#endif
            if (sign) { i = (UInt32)(-i); }
            return (int)(i);
        }


        // Computes the product of a fixpoint number with itself.
        static int fixsquare(int x)
        {
            UInt32 a;

            if (x < 0) { x = -x; }
            a = (((UInt32)x) >> FIXPOINT_PREC); x &= (int)~(a << FIXPOINT_PREC);
            x = (int)(((a * a) << FIXPOINT_PREC) + ((a * x) << 1) +
                ((UInt32)((x * x) + (1 << (FIXPOINT_PREC - 1))) >> FIXPOINT_PREC));
#if FIXPOINT_SIGN
  if (x < 0) {x ^= FIXPOINT_SIGN;}
#endif
            return (x);
        }


        // Computes the square root of a fixpoint number.
        static int fixsqrt(int x)
        {
            int test, step;

            if (x < 0) return (-1); if (x == 0) return (0);
            step = (x <= (1 << FIXPOINT_PREC)) ? (1 << FIXPOINT_PREC) : (1 << ((FIXPOINT_BITS - 2 + FIXPOINT_PREC) >> 1));
            test = 0;
            while (step != 0)
            {
                int h;

                h = fixsquare(test + step);
                if (h <= x) { test += step; }
                if (h == x) break;
                step >>= 1;
            }
            return (test);
        }


        // Divides a fixpoint number by another fixpoint number, yielding a fixpoint result.
        static int fixdiv(int x, int y)
        {
            int res, mask;
            bool sign;

            sign = (x ^ y) < 0;
            if (x < 0) { x = -x; }
            if (y < 0) { y = -y; }
            mask = (1 << FIXPOINT_PREC); res = 0;
            while (x > y) { y <<= 1; mask <<= 1; }
            while (mask != 0)
            {
                if (x >= y) { res |= mask; x -= y; }
                mask >>= 1; y >>= 1;
            }
#if FIXPOINT_SIGN
  if (res < 0) {res ^= FIXPOINT_SIGN;}
#endif
            if (sign) { res = -res; }
            return (res);
        }


        #endregion static functions


        /*
 *  The C++ Fixpoint class. By no means exhaustive...
 *  Since it contains only one int data, variables of type FixPoint can be
 *  passed directly rather than as a reference.
 */


        /*
         *  int gets treated differently according to the case:
         *
         *  a) Equations (=) or condition checks (==, <, <= ...): raw int (i.e. no conversion)
         *  b) As an argument for an arithmetic operation: conversion to fixpoint by shifting
         *
         *  Otherwise loading meaningful values into FixPoint variables would be very awkward.
         */

        FixPoint() { x = 0; }

        FixPoint(int y) { x = y; }

        ~FixPoint() { ;}

        int Value() { return (x); }

        int round() { return ((x + (1 << (FIXPOINT_PREC - 1))) >> FIXPOINT_PREC); }

        public static implicit operator int(FixPoint f) { return (f.x); }

        public static implicit operator FixPoint(int i) { return new FixPoint(i); }

        public static implicit operator FixPoint(long i) { return new FixPoint((int)i); }

        public static implicit operator FixPoint(float i) { return new FixPoint((int)i); }


        // unary operators
        public FixPoint sqrt() { return (fixsqrt(x)); }

        public FixPoint sqr() { return (fixsquare(x)); }

        public FixPoint abs() { return ((x < 0) ? -x : x); }

        //public static FixPoint operator+() {return(x);}

        //public static FixPoint operator-() {return(-x);}

        public static FixPoint operator ++(FixPoint x) { x.x += (1 << FIXPOINT_PREC); return x; }

        public static FixPoint operator --(FixPoint x) { x.x -= (1 << FIXPOINT_PREC); return x; }


        // binary operators
        public int imul(int y) { return (intmult(x, y)); }

        //public static implicit operator FixPoint(FixPoint from) { return new FixPoint(from.x); }

        //public static FixPoint operator=(int y) {x = y; return x;}

        public static FixPoint operator +(FixPoint x, FixPoint y) { x.x += y.Value(); return x; }

        public static FixPoint operator +(FixPoint x, int y) { x.x += (y << FIXPOINT_PREC); return x; }

        public static FixPoint operator -(FixPoint x, FixPoint y) { x.x -= y.Value(); return x; }

        public static FixPoint operator -(FixPoint x, int y) { x.x -= (y << FIXPOINT_PREC); return x; }

        public static FixPoint operator *(FixPoint x, FixPoint y) { x.x = fixmult(x, y.Value()); return x; }

        public static FixPoint operator *(FixPoint x, int y) { x.x *= y; return x; }

        public static FixPoint operator /(FixPoint x, FixPoint y) { x.x = fixdiv(x, y.Value()); return x; }

        public static FixPoint operator /(FixPoint x, int y) { x.x /= y; return x; }

        public static FixPoint operator <<(FixPoint x, int y) { x.x <<= y; return x; }

        public static FixPoint operator >>(FixPoint x, int y) { x.x >>= y; return x; }


        // conditional operators
        public static bool operator <(FixPoint x, FixPoint y) { return (x.x < y.Value()); }

        public static bool operator <(FixPoint x, int y) { return (x.x < y); }

        public static bool operator <=(FixPoint x, FixPoint y) { return (x.x <= y.Value()); }

        public static bool operator <=(FixPoint x, int y) { return (x.x <= y); }

        public static bool operator >(FixPoint x, FixPoint y) { return (x.x > y.Value()); }

        public static bool operator >(FixPoint x, int y) { return (x.x > y); }

        public static bool operator >=(FixPoint x, FixPoint y) { return (x.x >= y.Value()); }

        public static bool operator >=(FixPoint x, int y) { return (x.x >= y); }

        public static bool operator ==(FixPoint x, FixPoint y) { return (x.x == y.Value()); }

        public static bool operator ==(FixPoint x, int y) { return (x.x == y); }

        public static bool operator !=(FixPoint x, FixPoint y) { return (x.x != y.Value()); }

        public static bool operator !=(FixPoint x, int y) { return (x.x != y); }



        /*
         *  In case the first argument is an int (i.e. member-operators not applicable):
         *  Not supported: things like int/FixPoint. The same difference in conversions
         *  applies as mentioned above.
         */


        // binary operators
        public static FixPoint operator +(int x, FixPoint y) { return ((x << FIXPOINT_PREC) + y.Value()); }

        public static FixPoint operator -(int x, FixPoint y) { return ((x << FIXPOINT_PREC) - y.Value()); }

        public static FixPoint operator *(int x, FixPoint y) { return (x * y.Value()); }


        // conditional operators
        public static bool operator ==(int x, FixPoint y) { return (x == y.Value()); }

        public static bool operator !=(int x, FixPoint y) { return (x != y.Value()); }

        public static bool operator <(int x, FixPoint y) { return (x < y.Value()); }

        public static bool operator <=(int x, FixPoint y) { return (x <= y.Value()); }

        public static bool operator >(int x, FixPoint y) { return (x > y.Value()); }

        public static bool operator >=(int x, FixPoint y) { return (x >= y.Value()); }



        /*
         *  For more convenient creation of constant fixpoint numbers from constant floats.
         */

        public static FixPoint FixNo(int n) { return (FixPoint)((int)(n * (1 << FIXPOINT_PREC))); }
        public static FixPoint FixNo(double n) { return (FixPoint)((int)(n * (1 << FIXPOINT_PREC))); }






        /*
         *  Stuff re. the sinus table used with fixpoint arithmetic
         */


        // define as global variable
        static FixPoint[] SinTable = new FixPoint[(1 << ldSINTAB)];


        //#define FIXPOINT_SIN_COS_GENERIC \
        //  if (angle >= 3*(1<<ldSINTAB)) {return(-SinTable[(1<<(ldSINTAB+2)) - angle]);}\
        //  if (angle >= 2*(1<<ldSINTAB)) {return(-SinTable[angle - 2*(1<<ldSINTAB)]);}\
        //  if (angle >= (1<<ldSINTAB)) {return(SinTable[2*(1<<ldSINTAB) - angle]);}\
        //  return(SinTable[angle]);


        // sin and cos: angle is fixpoint number 0 <= angle <= 2 (*PI)
        public static FixPoint fixsin(FixPoint x)
        {
            int angle = x;

            angle = (angle >> (FIXPOINT_PREC - ldSINTAB - 1)) & ((1 << (ldSINTAB + 2)) - 1);

            // FIXPOINT_SIN_COS_GENERIC macro
            if (angle >= 3 * (1 << ldSINTAB)) { return (-SinTable[(1 << (ldSINTAB + 2)) - angle]); }
            if (angle >= 2 * (1 << ldSINTAB)) { return (-SinTable[angle - 2 * (1 << ldSINTAB)]); }
            if (angle >= (1 << ldSINTAB)) { return (SinTable[2 * (1 << ldSINTAB) - angle]); }
            return (SinTable[angle]);
        }

        public static FixPoint fixcos(FixPoint x)
        {
            int angle = x;

            // cos(x) = sin(x+PI/2)
            angle = (angle + (1 << (FIXPOINT_PREC - 1)) >> (FIXPOINT_PREC - ldSINTAB - 1)) & ((1 << (ldSINTAB + 2)) - 1);

            // FIXPOINT_SIN_COS_GENERIC macro
            if (angle >= 3 * (1 << ldSINTAB)) { return (-SinTable[(1 << (ldSINTAB + 2)) - angle]); }
            if (angle >= 2 * (1 << ldSINTAB)) { return (-SinTable[angle - 2 * (1 << ldSINTAB)]); }
            if (angle >= (1 << ldSINTAB)) { return (SinTable[2 * (1 << ldSINTAB) - angle]); }
            return (SinTable[angle]);

        }



        public static void InitFixSinTab()
        {
            int i;
            float step;

            for (i = 0, step = 0; i < (1 << ldSINTAB); i++, step += 0.5f / (1 << ldSINTAB))
            {
                SinTable[i] = FixNo(Math.Sin(Math.PI * step));
            }
        }

    }
}
