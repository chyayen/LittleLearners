using PagedList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ELearning.Models
{
    public class AdminViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalUsers { get; set; }
        public int TotalStories { get; set; }

        public List<StudentGradeModel> StudentGradeList { get; set; }
    }

    public class StudentGradeModel
    { 
        public string UserName { get; set; } = "";
        public int[] Grade { get; set; }
    }


    public class ResultModel
    {
        public bool success { get; set; } = false;
        public string message { get; set; } = "";
        public int id { get; set; } = 0;
    }

    public class ResultQuestionUpdateModel
    {
        public bool success { get; set; } = false;
        public string message { get; set; } = "";
        public QuestionModel question { get; set; } = new QuestionModel();
    }


    public class StudentSubmissionViewModel
    {
        public int StoryID { get; set; } = 0;
        public string StoryTitle { get; set; } = "";
        public IPagedList<StoryStudentAssignmentModel> Students { get; set; }
    }

    public class StoryStudentAssignmentModel
    {  
        public int StudentID { get; set; } = 0;
        public string StudentName { get; set; } = "";
        public int LatestAttempt { get; set; } = 0;
        public int TotalAttempts { get; set; } = 0;
    }

    public class LatestStudentSubmissionViewModel
    {
        public int StoryID { get; set; } = 0;
        public string StoryTitle { get; set; } = "";
        public int StudentID { get; set; } = 0;
        public int Attempt { get; set; } = 0;
        public int TotalQuestions { get; set; } = 0;
        public string Remarks { get; set; } = "";
        public List<LatestStudentSubmissionQuestionScoreModel> Questions { get; set; }
    }

    public class LatestStudentSubmissionQuestionScoreModel
    {
        public int AnswerID { get; set; } = 0;
        public int QuestionID { get; set; } = 0;
        public int StudentID { get; set; } = 0;

        [AllowHtml]
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
        public int StoryID { get; set; } = 0;
        public int Score { get; set; } = 0; 
        public DateTime DateAnswered { get; set; } = new DateTime();
        public int CheckedBy { get; set; } = 0;
        public DateTime DateChecked { get; set; } = new DateTime();
        public string MultipleChoiceCorrectAnswer { get; set; } = "";

        public List<AnswerModel> MultipleChoiceAnswers { get; set; }
    }

    public class StudentSubmissionHistoryViewModel
    {
        public int StoryID { get; set; } = 0;
        public string StoryTitle { get; set; } = "";
        public int StudentID { get; set; } = 0;
        public IPagedList<StudentSubmissionHistoryModel> StudentSubmissionHistories { get; set; }
    }

    public class StudentSubmissionHistoryModel
    {
        public string StoryTitleAndStudentCombination { get; set; } = "";
        public int StoryID { get; set; } = 0;
        public int Attempt { get; set; } = 0;
        public int TotalQuestions { get; set; } = 0;
        public int Grade { get; set; } = 0;
        public string Remarks { get; set; } = "";
        public string CheckedByName { get; set; } = "";
        public DateTime DateChecked { get; set; } = new DateTime();
    }

    public class StudentTrackingViewModel
    {
        public List<StudentTrackingModel> StudentTrackingList { get; set; }
        public List<StudentTrackingModel> StudentAssignedStoriesAndProgressList { get; set; }
    } 
     

    public class StudentRandomAnswerModel
    {
        public int StudentID { get; set; } = 0;
        public int StoryID { get; set; } = 0;
        public string StoryTitle { get; set; } = "";
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
        public DateTime DateAnswered { get; set; } = new DateTime();
    }

}