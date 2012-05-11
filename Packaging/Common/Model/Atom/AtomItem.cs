//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Common.Model.Atom {
    using System.Linq;
    using System.ServiceModel.Syndication;
    using System.Xml;
    using Toolkit.Extensions;

#if COAPP_ENGINE_CORE
    using Packaging.Service;
#endif

    public class AtomItem : SyndicationItem {
        public PackageModel Model { get; private set; }

        /// <summary>
        ///   This needs to be called after anything in the model is changed.
        /// </summary>
        public void SyncFromModel() {
            // this pulls down information from the Model element into the atom item.
            Id = Model.CanonicalName;
            Title = new TextSyndicationContent(Model.CosmeticName);
            Summary = new TextSyndicationContent(Model.PackageDetails.SummaryDescription);
            PublishDate = Model.PackageDetails.PublishDate;
            Authors.Clear();
            Contributors.Clear();
            Categories.Clear();
            Links.Clear();

            if (Model.PackageDetails.Publisher != null) {
                Authors.Add(CreatePerson().With(a => {
                    a.Name = Model.PackageDetails.Publisher.Name;
                    a.Email = Model.PackageDetails.Publisher.Email;
                    a.Uri = Model.PackageDetails.Publisher.Location == null ? string.Empty : Model.PackageDetails.Publisher.Location.ToString();
                }));
            }
            if (!Model.PackageDetails.Contributors.IsNullOrEmpty()) {
                foreach (var c in Model.PackageDetails.Contributors) {
                    var contributor = c;
                    Contributors.Add(CreatePerson().With(a => {
                        a.Name = contributor.Name;
                        a.Email = contributor.Email;
                        a.Uri = contributor.Location == null ? string.Empty : contributor.Location.ToString();
                    }));
                }
            }

            if (!string.IsNullOrEmpty(Model.PackageDetails.CopyrightStatement)) {
                Copyright = new TextSyndicationContent(Model.PackageDetails.CopyrightStatement);
            }

            if (!Model.PackageDetails.Tags.IsNullOrEmpty()) {
                foreach (var tag in Model.PackageDetails.Tags) {
                    Categories.Add(new SyndicationCategory(tag, "/Tags", tag));
                }
            }

            if (!Model.PackageDetails.Categories.IsNullOrEmpty()) {
                foreach (var category in Model.PackageDetails.Categories) {
                    Categories.Add(new SyndicationCategory(category, "/Categories", category));
                }
            }

            if (Model.PackageDetails.Description != null) {
                Content = SyndicationContent.CreateHtmlContent(Model.PackageDetails.Description);
            }

            if (!Model.Locations.IsNullOrEmpty()) {
                foreach (var l in Model.Locations) {
                    var location = l;
                    Links.Add(CreateLink().With(link => {
                        link.RelationshipType = "enclosure";
                        link.MediaType = "application/package";
                        link.Uri = location;
                        link.Title = Model.Name;
                    }));

                    Links.Add(CreateLink().With(link => {
                        link.Uri = location;
                    }));
                }
            }
            // and serialize that out.
            ElementExtensions.Add(Model, Model.XmlSerializer);
        }

        /// <summary>
        ///   This is only ever required after pulling values out of the AtomFeed XML, and is done automatically.
        /// </summary>
        private void SyncToModel() {
            // Model.ProductCode = Id; 
            Model.PackageDetails.SummaryDescription = Summary.Text;
            Model.PackageDetails.PublishDate = PublishDate.DateTime;

            var pub = Authors.FirstOrDefault();
            if (pub != null) {
                Model.PackageDetails.Publisher = new Identity {
                    Name = pub.Name,
                    Location = pub.Uri != null ? pub.Uri.ToUri() : null,
                    Email = pub.Email
                };
            }

            Model.PackageDetails.Contributors = Contributors.Select(each => new Identity {
                Name = each.Name,
                Location = each.Uri.ToUri(),
                Email = each.Email,
            }).ToXList();

            Model.PackageDetails.Tags = Categories.Where(each => each.Scheme == "/Tags").Select(each => each.Name).ToXList();
            Model.PackageDetails.Categories = Categories.Where(each => each.Scheme == "/Categories").Select(each => each.Name).ToXList();

            var content = (Content as TextSyndicationContent);
            Model.PackageDetails.Description = content == null ? string.Empty : content.Text;

            Model.PackageDetails.CopyrightStatement = Copyright == null ? string.Empty : Copyright.Text;

            Model.Locations = Links.Select(each => each.Uri.AbsoluteUri.ToUri()).Distinct().ToXList();
        }

        /// <summary>
        /// </summary>
        /// <param name="reader"> </param>
        /// <param name="version"> </param>
        /// <returns> When the reader parses the embedded package model we sync that back to the exposed model right away. </returns>
        protected override bool TryParseElement(XmlReader reader, string version) {
            try {
                var extension = Model.XmlSerializer.Deserialize(reader) as PackageModel;
                if (extension != null) {
                    Model = extension;
                    SyncToModel();
                }
                return Model != null;
            } catch {
                
            }
            return false;
        }

        public AtomItem() {
            Model = new PackageModel();
        }

        public AtomItem(PackageModel model) {
            Model = model;
        }

#if COAPP_ENGINE_CORE
        public AtomItem(Package package) {
            Model = new PackageModel {
                CanonicalName = package.CanonicalName, 
                DisplayName = package.DisplayName, 
                Vendor = package.Vendor, 
                BindingPolicy = package.BindingPolicy, 
                Roles = package.Roles,
                Dependencies = package.Dependencies.ToXDictionary(each => each.CanonicalName, each => each.FeedLocations), 
                Features = package.Features, 
                RequiredFeatures = package.RequiredFeatures, 
                Feeds = package.FeedLocations,
                Locations = package.RemoteLocations, 
                PackageDetails = package.PackageDetails
            };

            // when complete, 
            SyncFromModel();
        }

        /// <summary>
        ///   returns the package object for this element
        /// </summary>
        public Package Package {
            get {
                if( null == Model.CanonicalName) {
                    return null;
                }

                var package = Package.GetPackage(Model.CanonicalName);
                lock (package) {
                    // lets copy what details we have into that package.

                    if (!Model.Dependencies.IsNullOrEmpty()) {
                        package.Dependencies.AddRange(Model.Dependencies.Keys.Select(each => {
                            var result = Package.GetPackage(each);
                            result.FeedLocations.AddRangeUnique(Model.Dependencies[each]);
                            return result;
                        }));
                    }

                    package.DisplayName = Model.DisplayName;
                    package.Vendor = Model.Vendor;
                    package.BindingPolicy = Model.BindingPolicy;
                    package.Roles.AddRangeUnique(Model.Roles);
                    package.Features.AddRangeUnique(Model.Features);
                    package.RequiredFeatures.AddRangeUnique(Model.RequiredFeatures);
                    package.FeedLocations.AddRangeUnique(Model.Feeds);
                    package.RemoteLocations.AddRangeUnique( Model.Locations );
                    package.PackageDetails = Model.PackageDetails;
                }
                return package;
            }
        }
#endif
    }
}