using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#pragma warning disable

namespace ComicsViewer
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {

        /*
         * Bugs:
         * 2 Show search suggestions when opening an empty search box
         * 
         * TODOS:         
         * 3 Add the ability to save filters into a list of bookmarks
         * 4 Supporting opening stuff properly, rathor than telling Windows to open the first file 
         *   (see https://stackoverflow.com/a/44006005 or https://docs.microsoft.com/en-us/windows/uwp/winrt-components/brokered-windows-runtime-components-for-side-loaded-windows-store-apps)
         * 5 We will likely have to rely on the above two links to enable Python-based extensions.
         * 6 Make search history per-profile instead of per-app
         * 7 Create the AcceptableExceptions system, fixing the app crashing on FileNotFoundException, etc. 
         *   (e.g. opening an nonexistant comic)
         * 
         * Feature Requests:
         * 1 Grouping, and the ability to navigate to the start of a group (see Groove Music) (also this is apparently really hard)
         * 2 Reimplement the faded colors of the WPF version, ideally as a togglable setting
         * 3 To enable the above feature, we will need a settings pane for App-level (instead of / in addition to profile-level) settings.
         * 4 To enable progress tracking, we will probably need to implement a UWP-based image viewer too.
         *   This time we probably won't have to make it general-purpose.
         * 5 Add an expiration date to generated thumbnails, so they don't just pile up
         * 6 Assign "related works", allowing any two works to be related in the database and to show up in each other's 
         *   single-click flyout
         *   
         * Major proposal: subworks (and notes on related works)
         *   One decision of Comics.UWP is to remove subworks at the database level, instead treating each folder as a
         *   subwork when the user taps on a work. This means no more convoluted subwork naming systems and reimporting
         *   entire works when subworks change. But also, now we cannot search, or tag subworks. Only top-level works,
         *   those that exist in the view model and thus the database, can be searched, tagged, etc.
         *   
         *   We will have to design from the ground up to work with subworks. I have two proposals representing two
         *   extreme ranges of implementation.
         *   
         *   1. Minor proposal (subfolder as subworks)
         *      Each comic will have a list of subworks.
         *      +   class Subwork
         *      +       string Name : name of folder relative to comic path
         *      +       string DisplayName
         *          class Comic
         *      +       List<Subwork> Subworks
         *      Subworks do not have categories or tags. They simply can be searched, because we now know their name.
         *      All metadata still happens at the parent level.
         *      
         *      This is easy to implement, but perhaps a bit lacking: all this is, is caching subwork names to be searched.
         *   
         *   2. Major proposal (subworks as distinct works)
         *      Any comic at any location can be a main work or a subwork.
         *      +   enum ComicWorkType
         *      +       MainWork
         *      +       Subwork
         *          class Comic
         *      +       ComicWorkType WorkType
         *      +       List<Comic> Subworks
         *      When reading comics from disk, we will simply assign top-level works as main works, at second-level 
         *      works found in folders of top-level works as subworks, and automatically fill out the author and
         *      category labels. but the user is encouraged to change from the default and freely promote and demote works.
         *      
         *      Implementing this major proposal comes with the following implications that may require a substantial
         *      amount of code change:
         *        - the non-display versions of author and category may become meaningless. Since the author and
         *          category names of a work can no longer be determined from its path, and UniqueIdentifier is no longer
         *          usable. Perhaps it's in our best interest to restrict the ability to change a subwork's author/category
         *          names. 
         *      
         *   One of the main inspirations for a subworks system is to allow us to implement a related works system.
         *   For example, a comic might be released as part of a magazine or anthology, but also part of a series 
         *   published over time. We want to be able to properly store the comic as part of the magazine (i.e a
         *   subwork of the maganize), but also as part of its series (i.e. related to other works in the series).
         *      
         *   Therefore, a major consideration of how to implement subworks is how it will integrate with the other
         *   proposed feature of related works. I do not see a need to have subworks contain any more information than
         *   their path relative to their parent. We don't need the ability to detach subworks from main works. But we
         *   will need the ability to give subworks their own metadata, including potentially Author information (for
         *   works in magazines and anthologies; note that the current solution to this is to physically move the files
         *   out of a magazine and into the author's folder).
         *   
         *   Discussion on database structure:
         *      Major proposal
         *          CREATE TABLE comics
         *      +       comic_type INTEGER DEFAULT 1 CHECK (comic_type) IN (0, 1) -- where 0 is main work and 1 is subwork
         *              unique_name -- use : as the path separator?     
         *              title -- the data base doesn't know that unique_name == [author]title, so we can change the code
         *                       in Comics.UWP without affecting the database. That said we will have to think of another
         *                       way of generating the unique_name, if we can no longer dynamically generate it...
         *                       all in all, I think even with the major proposal we wlil need a new subworks table, but
         *                       this is just a proposal of how it might be done
         *      +   CREATE TABLE comic_related_works
         *      +       related_groupid INTEGER NOT NULL
         *      +       comicid INTEGER UNIQUE NOT NULL ON CONFLICT ABORT
         *              -- again, we might put the column comic_type here, and decide use to separate tables for main
         *                 and sub works.
         *              -- also, we will have to decide between 1-to-1 relations
         *                      i.e. group(a, b) -> a.related += b; b.related += 1
         *                 or group relations
         *                      i.e. group(a, b) -> b.group.mergewith(a.group).
         *                 the above example is based on group relations. either way we choose, we will need non-trivial
         *                 C# code. to create relationships.
         *             
         *      Minor proposal
         *      +   CREATE TABLE comic_subworks
         *      +       comicid INTEGER NOT NULL
         *      +       relative_path TEXT NOT NULL
         *      +       display_name TEXT NOT NULL
         *      +   CREATE TABLE comic_related_works
         *      +       comicid INTEGER NOT NULL
         *      +       subwork_name TEXT -- we can use null to represent the main work
         *      +       related_comicid INTEGER NOT NULL
         *      +       related_subwork_name INTEGER NOT NULL
         *      +       UNIQUE (comicid, subwork_name, related_comicid, related_subwork_name) ON CONFLICT IGNORE
         *      +       -- this is an example based on 1-to-1 relations
         */
        public static readonly Random Randomizer = new Random();

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // Ensure the current window is active
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
