using IntelReader.models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



namespace IntelReader
{
    public class FileMonitor{
        private List<LogFileInfo> monitorFiles;
        public bool threadMonitor { get; set; }
        public static event EventHandler<FileChanged> fileChanged;
        public List<string> looksFor;
        public string startPath;
        DirectoryInfo dir;
        public FileMonitor(){
            monitorFiles = new List<LogFileInfo>();
            looksFor = new List<string>();
            threadMonitor = false;
        }
        public void PopulateLogPool(){
             var directory = new DirectoryInfo(startPath);
                DateTime fromDate = DateTime.UtcNow.AddHours(-24);
                var datafiles = directory.GetFiles();
                var logFileInfo = new List<LogFileInfo>();
                foreach (var fi in datafiles)
                {
                    if (Utils.HasInFileName(fi.Name.ToLower(), config.logFileNames) && fi.LastWriteTimeUtc >= fromDate)
                    {
                        AddFile(fi.FullName, fi.LastWriteTimeUtc, GetSuffix(fi.FullName), fi.CreationTimeUtc);
                    }
                }

        }
        public void Off(){
            threadMonitor = false;
        }
        public void AddFile(string fn, DateTime lastWrite, Int32 suffix, DateTime createdTimeUtc){
            if(dir == null){
                dir = new DirectoryInfo(startPath);
                if(!threadMonitor){
                    threadMonitor = true;
                    ThreadPool.QueueUserWorkItem(StartMonitor);
                }
            }
            var exists = monitorFiles.Exists(a => a.fullName == fn);
            if(!exists){
                if (monitorFiles.Exists(a => a.name == GetFilePrefix(fn) && a.Created < createdTimeUtc && a.suffix < suffix))
                {
                    var j = monitorFiles.FirstOrDefault(a =>   a.name == GetFilePrefix(fn) && a.Created < createdTimeUtc && a.suffix < suffix);
                    monitorFiles.Remove(j);
                }
                monitorFiles.Add(new LogFileInfo(){lastWrite = lastWrite, fullName = fn, name=GetFilePrefix(fn), firstCheck = false, lines=0, suffix = suffix, Created = createdTimeUtc});
            }
        }
        
        protected virtual void OnFileChanged(FileChanged e)
        {
            EventHandler<FileChanged> handler = fileChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        private void StartMonitor(Object stateInfo){
            foreach (var fi in monitorFiles)
            {
                Console.WriteLine($"Monitoring: {fi.name}");
            }

            while(threadMonitor){
                Thread.Sleep(500);
                foreach(var di in dir.GetFiles()){
                    if(monitorFiles.Exists(a => a.fullName == di.FullName) ){
                        var lfi = monitorFiles.FirstOrDefault(a => a.fullName == di.FullName);
                        lfi.Created = di.CreationTimeUtc;
                        long currentLineCount;
                        using ( var fs = new FileStream(lfi.fullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)){
                            using (StreamReader sr = new StreamReader(fs))
                            {
                                currentLineCount = sr.ReadLineCount();
                            }

                        }
                        if(lfi.firstCheck && lfi.lines != currentLineCount){
                            // TODO activate event
                            Debug.WriteLine($"file changed: {lfi.name}");
                            var fc = new FileChanged(lfi.fullName, lfi.lines, lfi.prefix);
                            Debug.WriteLine($"Reading: {lfi.name} lines: {lfi.lines}  newlines{currentLineCount - lfi.lines}  fullName: {lfi.fullName}");
                            lfi.lines = currentLineCount;
                            
                            //await.Task.Run(Read.ReadLog(fc));
                            // Read.ReadLog(fc);
                            OnFileChanged(fc);
                            
                        }
                        lfi.lines = currentLineCount;
                        lfi.firstCheck = true;
                        lfi.lastWrite = di.LastWriteTimeUtc;
                        lfi.prefix =  GetFilePrefix(di.Name);

                        if(lfi.lastWrite < DateTime.UtcNow.AddDays(-1)){
                            monitorFiles.Remove(lfi);
                        }
                    }
                    
                }
            }
        }
        private string GetPath(string fn)    {
            int i = fn.LastIndexOf("\\");
            return fn.Substring(0,i);
        }

        private Int32 GetSuffix(string fn)
        {
            int f, dot;
            dot = fn.LastIndexOf(".");
            f = fn.LastIndexOf("_");
            f++;
            string rstring = fn.Substring(f, dot - f);
            return Convert.ToInt32(rstring);
        }
        private string GetFilePrefix(string fq)
        {
            int first, len;
            first = fq.LastIndexOf("\\");
            first++;
            len = fq.Substring(first).IndexOf("_");
            string rtn = fq.Substring(first, len);
            return rtn;
        }

        public void CheckChangedLogPool()
        {
            var candidatFiles = new List<LogFileInfo>();
            var directory = new DirectoryInfo(startPath);
            DateTime fromDate = DateTime.UtcNow.AddHours(-24);
            var datafiles = directory.GetFiles();
            var logFileInfo = new List<LogFileInfo>();
            foreach (var fi in datafiles)
            {
                if (Utils.HasInFileName(fi.Name.ToLower(), config.logFileNames) && fi.LastWriteTimeUtc >= fromDate)
                {
                    //.FullName, fi.LastWriteTimeUtc, GetSuffix(fi.FullName));
                    candidatFiles.Add( new LogFileInfo(){fullName = fi.FullName, lastWrite = fi.LastWriteTimeUtc, suffix = GetSuffix(fi.FullName), Created = fi.CreationTimeUtc});
                }
                var exists = candidatFiles.Exists(a => a.fullName == fi.FullName);
                if(!exists){
                    if (candidatFiles.Exists(a => a.name == GetFilePrefix(fi.FullName) && a.Created < fi.CreationTimeUtc && a.suffix < GetSuffix(fi.FullName)))
                    {
                        var j = monitorFiles.FirstOrDefault(a =>   a.name == GetFilePrefix(fi.FullName) && a.Created < fi.CreationTimeUtc && a.suffix < GetSuffix(fi.FullName));
                        candidatFiles.Remove(j);
                    }
                    candidatFiles.Add(new LogFileInfo(){lastWrite = fi.LastWriteTimeUtc, fullName = fi.FullName, name=GetFilePrefix(fi.FullName), firstCheck = false, lines=0, suffix = GetSuffix(fi.FullName), Created = fi.CreationTimeUtc});
                }
            }

            foreach (var can in candidatFiles)
            {
                CheckCandidate(can);
            }

        }

        private void CheckCandidate(LogFileInfo lfi)
        {
            LogFileInfo dmon = null;
            foreach (var mon in monitorFiles)
            {
                if (lfi.fullName != mon.fullName)
                {
                    dmon = mon;
                    monitorFiles.Add(lfi);
                }

                break;
            }
            if(dmon != null)
                monitorFiles.Remove(dmon);
        }
    }
}
