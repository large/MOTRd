using DM.MovieApi;
using DM.MovieApi.ApiResponse;
using DM.MovieApi.MovieDb.Movies;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using WebSockets.Common;
using System.ComponentModel;
using System.Reflection;
using System.IO;
using fastJSON;

namespace MOTRd
{
    //LiteDB class for storage, use the class that will be filled 
    public class MovieInformation : MovieInfo
    {
        public MovieInformation() { }
        public MovieInformation(MovieInfo movie)
        {
            Type t = typeof(MovieInfo);
            PropertyInfo[] properties = t.GetProperties();
            foreach (PropertyInfo pi in properties)
            {
                if (pi.Name != "Genres")
                    pi.SetValue(this, pi.GetValue(movie, null), null);
                else
                {
                    if (movie.Genres.Count > 0)
                    {
                        this.Genres = new string[movie.Genres.Count];
                        for (int i = 0; i < movie.Genres.Count; i++)
                            this.Genres[i] = movie.Genres[i].Name;
                    }
                }
            }

        }

        //Variables xtra for the storage
        public new string[] Genres { get; set; }
        public int _id { get; set; }
        public DateTime Added { get; set; }
        public string Path { get; set; }
    }

    //Setings for the TMDB
    public class MOTRMovieDbSettings : IMovieDbSettings
    {
        // implementation
        // Summary:
        //     Private key required to query themoviedb.org API.
        public string ApiKey { get; set; }
        //
        // Summary:
        //     URL used for api calls to themoviedb.org.
        //     Current URL is: http://api.themoviedb.org/3/
        public string ApiUrl { get; set; }
    }

    //Actually class for handling scraping
    public class MOTR_Moviescraper
    {
        private MOTRMovieDbSettings MOTRScrapperSettings;
        private ApiSearchResponse<MovieInfo> apiSearchResponse;
        private readonly IWebSocketLogger _logger;
        public MovieInformation movieInformation { get; set; }

        public MOTR_Moviescraper(IWebSocketLogger logger)
        {
            //Set the settings for the scrapper
            MOTRScrapperSettings = new MOTRMovieDbSettings();
            MOTRScrapperSettings.ApiKey = "73706c8ed57633aeb73e866cd896ff9c";
            MOTRScrapperSettings.ApiUrl = "http://api.themoviedb.org/3/";

            // registration with an implementation of IMovieDbSettings
            MovieDbFactory.RegisterSettings(MOTRScrapperSettings);

            //Store logger at startup
            _logger = logger;
            movieInformation = null;
        }

        public ArrayList Query(string sQuery)
        {
            // as the factory returns a Lazy<T> instance, simply grab the Value out of the Lazy<T>
            // and assign to a local variable.
            var movieApi = MovieDbFactory.Create<IApiMovieRequest>().Value;
            //ApiSearchResponse<MovieInfo> response = Task.Run(movieApi.SearchByTitleAsync("Star Trek")).result ;
            //apiSearchResponse = movieApi.SearchByTitleAsync(sQuery).Result; //Makes async sync...

            //apiSearchResponse = movieApi.SearchByTitleAsync(sQuery).GetAwaiter().GetResult();

            try
            {
                apiSearchResponse = Task.Run(() =>
                {
                        return movieApi.SearchByTitleAsync(sQuery);
                }).Result;
            }
            catch
            {
                return new ArrayList();
            }

            //Console.WriteLine("Movieimage: https://image.tmdb.org/t/p/original/" + apiSearchResponse.Results[0].PosterPath);

            ArrayList pArray = new ArrayList();
            foreach(MovieInfo pMovie in apiSearchResponse.Results)
            {
                DateTime dateTime = pMovie.ReleaseDate;
                pArray.Add(pMovie.OriginalTitle + " (" + dateTime.Year.ToString() + ")");
            }
            return pArray;
        }

        public bool Select(int nIdInfo)
        {
            //No love if we select something that is not valid
            if(nIdInfo<0 || nIdInfo >= apiSearchResponse.Results.Count)
                return false;

            //Store the movieinformation selected
            movieInformation = new MovieInformation(apiSearchResponse.Results[nIdInfo]);
            movieInformation.Added = DateTime.Now;

            //Now download posters
            this.DownloadImage(movieInformation.PosterPath);
            this.DownloadImage(movieInformation.BackdropPath);

            return true;
        }

        //Download function to get file 
        private void DownloadImage(string sImage)
        {
            //No file, just return
            if (sImage == null)
                return;
            if (sImage.Length == 0)
                return;

            //Get the path for files
            string desktopPath = MOTR_Settings.GetGlobalApplicationPath("MovieImages");
            if (!Directory.Exists(desktopPath))
                Directory.CreateDirectory(desktopPath);

            //No need to download again
            if (File.Exists(desktopPath + sImage))
                return;

            // This will download a large image from the web, you can change the value
            string urlstart = "https://image.tmdb.org/t/p/original/";
            string url = urlstart + sImage;

            //Using sync method
            using (var client = new WebClient())
            {
                client.DownloadFile(url, desktopPath + "/" + sImage);
            }

            /*
            //Async method for downloading
            using (WebClient wc = new WebClient())
            {
                //wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                wc.DownloadFileCompleted += wc_DownloadFileCompleted;
                wc.DownloadFileAsync(new Uri(url), desktopPath + "/" + sImage);
            }*/
        }

        /// <summary>
        ///  Show the progress of the download in a progressbar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /*private void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            // In case you don't have a progressBar Log the value instead
            _logger.Information(typeof(MOTR_Moviescraper), "MOTR_Moviescraper image download: " + e.ProgressPercentage.ToString());
        }*/
        
        //Feedback from the webclient, downloading our file to the imagestore
        private void wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                _logger.Error(typeof(MOTR_Moviescraper), "Download canceled!");
                return;
            }

            if (e.Error != null) // We have an error! Retry a few times, then abort.
            {
                _logger.Error(typeof(MOTR_Moviescraper), "An error accured during download: "+ e.Error.ToString());
                return;
            }

            _logger.Information(typeof(MOTR_Moviescraper), "MOTR_Moviescraper image download complete");
        }

        public ArrayList GetMovieArray()
        {
            //Now download posters
            this.DownloadImage(movieInformation.PosterPath);
            this.DownloadImage(movieInformation.BackdropPath);

            ArrayList arrayList = new ArrayList();
            string sPathRemove = movieInformation.Path;
            movieInformation.Path = "";

            string sMovieInfo = JSON.ToJSON(movieInformation);
            arrayList.Add(sMovieInfo);

            movieInformation.Path = sPathRemove;
            return arrayList;
        }

    }
}
