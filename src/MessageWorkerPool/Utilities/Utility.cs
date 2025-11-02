// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace MessageWorkerPool.Utilities
{
    internal class UtilityHelper
    {
        internal static int MaxPowerOfTwo(int number)
        {
            if (number <= 0)
                throw new ArgumentException("Number must be greater than 0.");
            var power = 1;
            while (power <= number)
            {
                power <<= 1;
            }

            return power >> 1;
        }
    }


}
