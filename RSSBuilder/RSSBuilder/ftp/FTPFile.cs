// edtFTPnet
// 
// Copyright (C) 2004 Enterprise Distributed Technologies Ltd
// 
// www.enterprisedt.com
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
// 
// Bug fixes, suggestions and comments should posted on 
// http://www.enterprisedt.com/forums/index.php
// 
// Change Log:
// 
// $Log: FTPFile.cs,v $
// Revision 1.8  2005/08/04 21:57:58  bruceb
// fixed comment
//
// Revision 1.7  2005/07/22 10:35:18  hans
// made CLS compliant for the purpose of strong naming
//
// Revision 1.6  2005/06/03 11:32:47  bruceb
// vms changes
//
// Revision 1.5  2005/02/07 17:19:13  bruceb
// constructor made public
//
// Revision 1.4  2004/11/05 20:00:28  bruceb
// cleaned up namespaces
//
// Revision 1.3  2004/10/29 09:41:44  bruceb
// removed /// in file header
//
//
//

using System;
using System.Globalization;
using System.Text;
    
namespace EnterpriseDT.Net.Ftp
{    
	/// <summary>  
	/// Represents a remote file (implementation)
	/// </summary>
	/// <author>
	/// Bruce Blackshaw       
	/// </author>
	/// <version>      
	/// $Revision: 1.8 $
	/// </version>
	public class FTPFile
	{
		/// <summary> 
		/// Get the type of file, eg UNIX
		/// </summary>
		/// <returns> the integer type of the file
		/// </returns>
		virtual public int Type
		{
			get
			{
				return type;
			}
		}
        
		/// <returns> 
		/// Returns the name.
		/// </returns>
		virtual public string Name
		{
			get
			{
				return fileName;
			}
			
		}
		/// <returns> 
		/// Returns the raw server string.
		/// </returns>
		virtual public string Raw
		{
			get
			{
				return rawRep;
			}
			
		}

        /// <returns> 
        /// Returns or sets the number of links to the file
		/// </returns>
		virtual public int LinkCount
		{
			get
			{
				return linkNum;
			}
			
			set
			{
				this.linkNum = value;
			}	
		}
        
        /// <returns> 
        /// Is this file a link
		/// </returns>
		virtual public bool Link
		{
			get
			{
				return isLink;
			}
			
			set
			{
				this.isLink = value;
			}	
		}
        
        
		/// <returns> 
		/// Returns the linked name.
		/// </returns>
		virtual public string LinkedName
		{
			get
			{
				return linkedFileName;
			}
			set
			{
				this.linkedFileName = value;
			}
		}
        
        
		/// <returns> 
		/// Gets or sets the group.
		/// </returns>
		virtual public string Group
		{
			get
			{
				return userGroup;
			}
			set
			{
				this.userGroup = value;
			}
		}        
        
		/// <returns> 
		/// Gets or sets the owner.
		/// </returns>
		virtual public string Owner
		{
			get
			{
				return fileOwner;
			}
			set
			{
				this.fileOwner = value;
			}
		}   
        
        
        /// <returns> 
		/// Gets or sets whether this is a directory
		/// </returns>
		virtual public bool Dir
		{
			get
			{
				return isDir;
			}
			set
			{
				this.isDir = value;
			}
		}      
        
		/// <returns> 
		/// Gets or sets the permissions.
		/// </returns>
		virtual public string Permissions
		{
			get
			{
				return filePermissions;
			}
			set
			{
				this.filePermissions = value;
			}
		} 
        
		/// <returns> 
		/// Gets last modified timestamp
		/// </returns>
		virtual public DateTime LastModified
		{
			get
			{
				return lastModifiedTime;
			}
		} 
        
        
		/// <returns> 
		/// Gets size of file
		/// </returns>
		virtual public long Size
		{
			get
			{
				return fileSize;
			}
		}      
		
		/// <summary> Unknown remote server type</summary>
		public const int UNKNOWN = - 1;
		
		/// <summary> Windows type</summary>
		public const int WINDOWS = 0;
		
		/// <summary> UNIX type</summary>
		public const int UNIX = 1;

		/// <summary> VMS type</summary>
		public const int VMS = 2;

        /// <summary>Date format</summary>
		private static readonly string format = "dd-MM-yyyy HH:mm";
		
		/// <summary> Type of file</summary>
		private int type;
		
		/// <summary> Is this file a symbolic link?</summary>
		protected internal bool isLink = false;
		
		/// <summary> Number of links to file</summary>
		protected internal int linkNum = 1;
		
		/// <summary> Permission bits string</summary>
		protected internal string filePermissions;
		
		/// <summary> Is this a directory?</summary>
		protected internal bool isDir = false;
		
		/// <summary> Size of file</summary>
		protected internal long fileSize = 0L;
		
		/// <summary> File/dir name</summary>
		protected internal string fileName;
		
		/// <summary> Name of file this is linked to</summary>
		protected internal string linkedFileName;
		
		/// <summary> Owner if known</summary>
		protected internal string fileOwner;
		
		/// <summary> Group if known</summary>
		protected internal string userGroup;
		
		/// <summary> Last modified</summary>
		protected internal System.DateTime lastModifiedTime;
		
		/// <summary> Raw string</summary>
		protected internal string rawRep;
		
		/// <summary> 
		/// Constructor
		/// </summary>
		/// <param name="type">         
		/// type of file
		/// </param>
		/// <param name="raw">          
		/// raw string returned from server
		/// </param>
		/// <param name="name">         
		/// name of file
		/// </param>
		/// <param name="size">         
		/// size of file
		/// </param>
		/// <param name="isDir">        
		/// true if a directory
		/// </param>
		/// <param name="lastModifiedTime"> 
		/// last modified timestamp
		/// </param>
		public FTPFile(int type, string raw, string name, long size, 
                         bool isDir, ref DateTime lastModifiedTime)
		{
			this.type = type;
			this.rawRep = raw;
			this.fileName = name;
			this.fileSize = size;
			this.isDir = isDir;
			this.lastModifiedTime = lastModifiedTime;
		}
		
		/// <summary> 
		/// Constructor
		/// </summary>
		/// <param name="name">         
		/// name of file
		/// </param>
		/// <param name="size">         
		/// size of file
		/// </param>
		/// <param name="isDir">        
		/// true if a directory
		/// </param>
		/// <param name="lastModifiedTime"> 
		/// last modified timestamp
		/// </param>
		internal FTPFile(string name, long size, bool isDir, DateTime lastModifiedTime)
		{
			this.type = UNKNOWN;
			this.rawRep = "";
			this.fileName = name;
			this.fileSize = size;
			this.isDir = isDir;
			this.lastModifiedTime = lastModifiedTime;
		}
		
		/// <returns> 
		/// string representation
		/// </returns>
		public override string ToString()
		{
			StringBuilder buf = new StringBuilder(rawRep);
			buf.Append("[").Append("Name=").Append(fileName).Append(",").Append("Size=").
                Append(fileSize).Append(",").Append("Permissions=").Append(filePermissions).
                Append(",").Append("Owner=").Append(fileOwner).Append(",").
                Append("Group=").Append(userGroup).Append(",").Append("Is link=").Append(isLink).
                Append(",").Append("Link count=").Append(linkNum).Append(",").
                Append("Is dir=").Append(isDir).Append(",").
                Append("Linked name=").Append(linkedFileName).Append(",").
                Append("Permissions=").Append(filePermissions).Append(",").
                Append("Last modified=").Append(lastModifiedTime.ToString(format, CultureInfo.CurrentCulture.DateTimeFormat)).
                Append("]");
			return buf.ToString();
		}
	}
}
