using System.Linq;
using cdeLib.Entities;
using MediatR;

namespace cdeLib.Upgrade
{
    public class UpgradeCommand : IRequest
    {
    }

    //Can't copy hashes, since we changed hashed alogorithim.
    public class MapV3ToV4Catalog
    {
        public RootEntry Map(cdeDataStructure3.Entities.RootEntry src)
        {
            var newRootEntry = new RootEntry
            {
                ActualFileName = src.ActualFileName,
                Description = src.Description,
                PathsWithUnauthorisedExceptions = src.PathsWithUnauthorisedExceptions,
                DefaultFileName = src.DefaultFileName,
                DriveLetterHint = src.DriveLetterHint,
                AvailSpace = src.AvailSpace,
                TotalSpace = src.TotalSpace,
                ScanStartUTC = src.ScanStartUTC,
                ScanEndUTC = src.ScanEndUTC,
                BitFields = (Flags) (byte) src.BitFields,
                // Hash = new Hash16
                // {
                //     HashA = src.Hash.HashA,
                //     HashB = src.Hash.HashB
                // },
                Modified = src.Modified,
                Size = src.Size,
                Path = src.Path
            };

            //do Children
            if (src.Children == null) return newRootEntry;
            foreach (var newDirEntry in src.Children.Select(MapDirEntry))
            {
                newRootEntry.AddChild(newDirEntry);
            }

            return newRootEntry;
        }

        private static DirEntry MapDirEntry(cdeDataStructure3.Entities.DirEntry src)
        {
            var newDirEntry = new DirEntry
            {
                Modified = src.Modified,
                // Hash = new Hash16
                // {
                //     HashA = src.Hash.HashA,
                //     HashB = src.Hash.HashB
                // },
                BitFields = (Flags) (byte) src.BitFields,
                Size = src.Size,
                Path = src.Path
            };

            if (src.Children == null) return newDirEntry;
            foreach (var newChild in src.Children.Select(MapDirEntry))
            {
                newDirEntry.AddChild(newChild);
            }

            return newDirEntry;
        }
    }
}