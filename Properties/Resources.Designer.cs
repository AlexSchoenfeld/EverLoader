﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace EverLoader.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("EverLoader.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] Build_EverSD_Folders {
            get {
                object obj = ResourceManager.GetObject("Build_EverSD_Folders", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap Button_Close_icon {
            get {
                object obj = ResourceManager.GetObject("Button-Close-icon", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap Button_Close_icon_small {
            get {
                object obj = ResourceManager.GetObject("Button-Close-icon_small", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] crc2rom_mappings {
            get {
                object obj = ResourceManager.GetObject("crc2rom_mappings", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap green {
            get {
                object obj = ResourceManager.GetObject("green", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap Logo___EverSD {
            get {
                object obj = ResourceManager.GetObject("Logo - EverSD", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] mamenames {
            get {
                object obj = ResourceManager.GetObject("mamenames", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap NoBannerArt {
            get {
                object obj = ResourceManager.GetObject("NoBannerArt", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap NoBoxArtLarge {
            get {
                object obj = ResourceManager.GetObject("NoBoxArtLarge", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap NoBoxArtMedium {
            get {
                object obj = ResourceManager.GetObject("NoBoxArtMedium", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap NoBoxArtSmall {
            get {
                object obj = ResourceManager.GetObject("NoBoxArtSmall", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap red {
            get {
                object obj = ResourceManager.GetObject("red", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to #!/bin/bash -x
        ///
        ///SCRIPT_DIR=$(dirname &quot;$BASH_SOURCE&quot;)
        ///
        ////usr/bin/blastretro &quot;${SCRIPT_DIR}/../{CORE_FILENAME}&quot; &quot;${SCRIPT_DIR}/../game/{ROM_FILENAME}&quot;.
        /// </summary>
        internal static string special_bash {
            get {
                return ResourceManager.GetString("special_bash", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to #!/bin/bash -x
        ///
        ///SCRIPT_DIR=$(dirname &quot;$BASH_SOURCE&quot;)
        ///cd &quot;${SCRIPT_DIR}/../retroarch&quot;
        ///
        ///RA_PATH=/usr/bin/retroarch2
        ///if [ -f retroarch ]; then
        ///  cp -u retroarch /tmp
        ///  chmod +x /tmp/retroarch
        ///  RA_PATH=/tmp/retroarch
        ///fi
        ///
        ///RA_CONFIG=
        ///if [ &quot;$(cat /etc/rootfs_version | grep -i VS)&quot; ]; then
        ///  RA_CONFIG=retroarch_vs.cfg
        ///else
        ///  RA_CONFIG=retroarch.cfg
        ///fi
        ///
        ///${RA_PATH} -c &quot;./config/${RA_CONFIG}&quot; -L &quot;./cores/{CORE_FILENAME}&quot; &quot;../roms/{ROM_FILENAME}&quot;.
        /// </summary>
        internal static string special_bash_ra {
            get {
                return ResourceManager.GetString("special_bash_ra", resourceCulture);
            }
        }
    }
}
