#!/bin/bash
set -e
cd "$(dirname "$0")"
g++ -c -nodefaultlibs -fno-rtti -fno-exceptions -lc io.cpp -o io.o
(
echo '// <auto-generated />
namespace KJU.Application
{
    using System;

    public static class BundledStdlib {
          public static byte[] data = Convert.FromBase64String(@"'
base64 < io.o
echo '");
     }
}'
) > ../KJU.Application/BundledStdlib.cs
