using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NDesk.Options;
using Sep.Git.Tfs.Core;
using Sep.Git.Tfs.Core.TfsInterop;
using StructureMap;

namespace Sep.Git.Tfs.Commands
{
    [Pluggable("shelve-convert")]
    [Description("shelve-convert -u <shelve-owner-name> [options]")]
    [RequiresValidGitRepository]
    public class ShelveConvert : GitTfsCommand
    {
        private readonly TextWriter _stdout;
        private readonly Globals _globals;

        public bool Force { get; set; }
        public string Owner { get; set; }

        public OptionSet OptionSet
        {
            get
            {
                return new OptionSet
                {
                    { "force", "Get as much of the Shelveset as possible, and log any other errors",
                        v => Force = v != null },
                    { "u|user=", "Shelveset owner (default: current user)\nUse 'all' to get all shelvesets.",
                        v => Owner = v },
                };
            }
        }

        public ShelveConvert(TextWriter stdout, Globals globals)
        {
            _stdout = stdout;
            _globals = globals;
        }

        public int Run()
        {
            var remote = _globals.Repository.ReadTfsRemote(_globals.RemoteId);
            //var remote = _globals.Repository.ReadTfsRemote(TfsBranch);

            //return remote.Tfs.ListShelvesets(this, remote);

            var pathToRemote = _globals.Repository.ReadAllTfsRemotes().ToDictionary(r => r.TfsRepositoryPath);

            foreach (var item in remote.Tfs.GetShelvesets(remote, Owner))
            {
                DoUnshelve(item, pathToRemote);
            }

            return GitTfsExitCodes.OK;
        }

        private void DoUnshelve(ShelvesetInfo shelveset, IDictionary<string, IGitTfsRemote> pathToRemote)
        {
            var shelvesetName = shelveset.Name;
            if (!pathToRemote.ContainsKey(shelveset.Branch))
            {
                _stdout.WriteLine("Skipped " + shelveset.Branch + " from shelveset \"" + shelvesetName + "\".");
                return;
            }
            var remote = pathToRemote[shelveset.Branch];
            var escapedName = EscapeRef(shelvesetName);
            var owner = shelveset.Owner;
            var ownerDomainIndex = owner.LastIndexOf('\\');
            if (ownerDomainIndex >= 0 && ownerDomainIndex + 1 < owner.Length)
                owner = owner.Substring(ownerDomainIndex + 1);
            var escapedOwner = EscapeRef(owner);
            var destinationBranch = string.Format("refs/shelvesets/{0}/{1}", escapedOwner, escapedName);

            var destinationRef = GitRepository.ShortToLocalName(destinationBranch);
            if (_globals.Repository.HasRef(destinationRef))
            {
                _stdout.WriteLine("Skipping: Destination branch (" + destinationBranch + ") already exists!");
                return;
            }

            remote.Unshelve(Owner, shelvesetName, destinationBranch, BuildErrorHandler(), Force);
            _stdout.WriteLine("Created branch " + destinationBranch + " from shelveset \"" + shelvesetName + "\".");
        }

        private string EscapeRef(string name)
        {
            return Regex.Replace(name, "[^a-zA-Z0-9]", "_");
        }

        private Action<Exception> BuildErrorHandler()
        {
            if (Force)
            {
                return (e) =>
                {
                    _stdout.WriteLine("WARNING: unshelve: " + e.Message);
                    Trace.WriteLine(e);
                };
            }
            return null;
        }
    }
}
