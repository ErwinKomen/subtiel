using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace omdb {
  class MovieInfo {
    private String loc_title = "";
    private String loc_year = "";
    private String loc_runtime = "";
    private String loc_genre = "";
    private String loc_rated = "";
    private String loc_released = "";
    private String loc_director = "";
    private String loc_writer = "";
    private String loc_actors = "";
    private String loc_plot = "";
    private String loc_language = "";
    private String loc_country = "";
    private String loc_awards = "";
    private String loc_imdbRating = "";
    private String loc_imdbVotes = "";
    private String loc_type = "";
    public MovieInfo() {
      // No real action here...
    }
    // ================== property getters and setters ========================
    public String title { get { return loc_title; } set { loc_title = value; } }
    public String year { get { return loc_year; } set { loc_year = value; } }
    public String runtime { get { return loc_runtime; } set { loc_runtime = value; } }
    public String genre { get { return loc_genre; } set { loc_genre = value; } }
    public String rated { get { return loc_rated; } set { loc_rated = value; } }
    public String released { get { return loc_released; } set { loc_released = value; } }
    public String director { get { return loc_director; } set { loc_director = value; } }
    public String writer { get { return loc_writer; } set { loc_writer = value; } }
    public String actors { get { return loc_actors; } set { loc_actors = value; } }
    public String plot { get { return loc_plot; } set { loc_plot = value; } }
    public String language { get { return loc_language; } set { loc_language = value; } }
    public String country { get { return loc_country; } set { loc_country = value; } }
    public String imdbRating { get { return loc_imdbRating; } set { loc_imdbRating = value; } }
    public String imdbVotes { get { return loc_imdbVotes; } set { loc_imdbVotes = value; } }
    public String awards { get { return loc_awards; } set { loc_awards = value; } }
    public String type { get { return loc_type; } set { loc_type = value; } }
  }
}
