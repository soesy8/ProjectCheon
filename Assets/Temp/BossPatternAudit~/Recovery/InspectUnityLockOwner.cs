using System;
using System.Runtime.InteropServices;
using System.Text;
public static class InspectUnityLockOwner
{
    [StructLayout(LayoutKind.Sequential)] public struct UniqueProcess {public int processId;public System.Runtime.InteropServices.ComTypes.FILETIME startTime;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct ProcessInfo
    {
        public UniqueProcess process;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string application;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string service;
        public uint type,status,session;[MarshalAs(UnmanagedType.Bool)]public bool restartable;
    }
    [DllImport("rstrtmgr.dll",CharSet=CharSet.Unicode)]static extern int RmStartSession(out uint handle,int flags,StringBuilder key);
    [DllImport("rstrtmgr.dll",CharSet=CharSet.Unicode)]static extern int RmRegisterResources(uint handle,uint files,string[] paths,uint applications,UniqueProcess[] processes,uint services,string[] names);
    [DllImport("rstrtmgr.dll")]static extern int RmGetList(uint handle,out uint required,ref uint count,[In,Out] ProcessInfo[] info,ref uint reasons);
    [DllImport("rstrtmgr.dll")]static extern int RmEndSession(uint handle);
    public static ProcessInfo[] Read(string path)
    {
        uint handle;int result=RmStartSession(out handle,0,new StringBuilder(33));if(result!=0)throw new Exception("Session error "+result);
        try
        {
            result=RmRegisterResources(handle,1,new[]{path},0,null,0,null);if(result!=0)throw new Exception("Registration error "+result);
            uint required,count=0,reasons=0;result=RmGetList(handle,out required,ref count,null,ref reasons);
            if(result==0)return new ProcessInfo[0];if(result!=234)throw new Exception("List error "+result);
            var items=new ProcessInfo[required];count=required;result=RmGetList(handle,out required,ref count,items,ref reasons);
            if(result!=0)throw new Exception("List error "+result);return items;
        }
        finally{RmEndSession(handle);}
    }
}
