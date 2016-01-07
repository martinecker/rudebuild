RudeBuild, Version 1.4
----------------------

A bulk/unity C++ build tool for Visual Studio, developed by Martin Ecker.
This is free, open source software under the zlib license.

For more information and latest updates please visit:
http://rudebuild.sourceforge.net

----------------------

RudeBuild is a non-intrusive bulk/unity C++ build tool that seamlessly integrates into Visual Studio 2008, 2010, 2012, and 2013 as an add-in and into Visual Studio 2015 as extension.  It can speed up build times of large C++ projects by a factor of 5 or more.  RudeBuild also supports the use of IncrediBuild to speed up your build times even more.

RudeBuild comes in two flavors, as a command-line tool that works on Visual Studio solution files and as a Visual Studio add-in complete with toolbar and menus.

When used as an add-in the toolbar acts and looks just like the regular build toolbar but behind the scenes a bulk/unity build of C++ projects is triggered, automatically combining the .cpp files into unity files in a cache location and running devenv to build the modified solution/project.  Using RudeBuild in this manner is transparent to the developer and requires no modification to the original source code or project files whatsoever given that the codebase is bulk/unity build-safe.  Being bulk/unity-build safe means that there are no symbols with the same name in two different translation units.  For example, it is invalid to have a static function called GetFileTime in both File1.cpp and File2.cpp.

The command line version of RudeBuild is useful for automated builds, for example on build servers.  A solution file, build configuration and optionally a project name must be specified on the command line.

----------------------

RudeBuild is written in C# and requires the .NET framework 3.5 or higher.
RudeBuild uses the CommandLineParser library that is available at https://commandline.codeplex.com as well as Wix# to generate the MSI setup available at https://wixsharp.codeplex.com.

