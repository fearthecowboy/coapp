using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.toolkit.Engine {
    interface IPackageManager {
        void FindPackages(string canonicalName, string name, string version, string arch, string publicKeyToken,
            bool? dependencies, bool? installed, bool? active, bool? required, bool? blocked, bool? latest,
            int? index, int? maxResults, string location, bool? forceScan, bool? updates, bool? upgrades, bool? trimable);

        void GetPackageDetails(string canonicalName);
        void InstallPackage(string canonicalName, bool? autoUpgrade, bool? force, bool? download, bool? pretend, bool? isUpdating, bool? isUpgrading);
        void DownloadProgress(string canonicalName, int? downloadProgress);
        void ListFeeds(int? index, int? maxResults);
        void RemoveFeed(string location, bool? session);
        void AddFeed(string location, bool? session);
        void VerifyFileSignature(string filename);
        void SetPackage(string canonicalName, bool? active, bool? required, bool? blocked, bool? doNotUpdate, bool? doNotUpgrade);
        void RemovePackage(string canonicalName, bool? force);
        void UnableToAcquire(string canonicalName);
        void RecognizeFile(string canonicalName, string localLocation, string remoteLocation);
        void SetFeedFlags(string location, string activePassiveIgnored);
        void SuppressFeed(string location);

    }
}
