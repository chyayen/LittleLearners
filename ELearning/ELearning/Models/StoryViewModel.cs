using PagedList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ELearning.Models
{ 
    public class StoryViewModel
    {
        public IPagedList<StoryModel> Stories { get; set; }
    } 

    public class StoryModel
    {
        public int ID { get; set; } = 0;
        public string Title { get; set; } = ""; 
        public string CoverImageName { get; set; } = "";
        public string CoverImagePath { get; set; } = "";

        [AllowHtml]
        public string Content { get; set; } = "";
        public int ClassID { get; set; } = 0;
        public string ClassName { get; set; } = "";
        public int AddedBy { get; set; } = 0;
        public DateTime DateAdded { get; set; } = new DateTime();
        public int UpdatedBy { get; set; } = 0;
        public DateTime DateUpdated { get; set; } = new DateTime();
        public int DeletedBy { get; set; } = 0;
        public DateTime DateDeleted { get; set; } = new DateTime();
        public bool IsDeleted { get; set; } = false;
        public int CheckedBy { get; set; } = 0;
        public string AddedByName { get; set; } = "";
        public int QuizAttempt { get; set; } = 0;
        public bool AllowToRetake { get; set; } = false;
        public int[] SelectedSectionIds { get; set; }
        public int[] SelectedStudentIds { get; set; }
        public bool Incomplete { get; set; } = false;
        public string RandomQuestion { get; set; } = "";
        public string RandomAnswerOption1 { get; set; } = "";
        public string RandomAnswerOption2 { get; set; } = "";
        public string RandomAnswerOption3 { get; set; } = "";
        public string RandomAnswerOption4 { get; set; } = "";
        public string RandomCorrectAnswer { get; set; } = "";
        public string RandomQuestionHint { get; set; } = "";
        public string Subtitle { get; set; } = "";

        public IEnumerable<SelectListItem> ClassList { get; set; }
        public List<PageContent> PageContents { get; set; } = new List<PageContent>();

        public StudentProgressModel Progress { get; set; }
    }

    public class PageContent
    {
        public int PageNumber { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
    }

    public class StoryFileUploadViewModel
    {
        public HttpPostedFileBase File { get; set; }
        public HttpPostedFileBase CoverPage { get; set; }
        public int ClassID { get; set; } = 0;
        public string SelectedSectionIds { get; set; }
        public string SelectedStudentIds { get; set; }
        public bool Incomplete { get; set; } = false;
        public string Subtitle { get; set; } = "";
        public IEnumerable<SelectListItem> ClassList { get; set; }
    }

    public class QuestionViewModel
    {
        public int StoryID { get; set; } = 0;
        public string StoryTitle { get; set; } = "";
        public List<QuestionModel> Questions { get; set; }
        //public IPagedList<QuestionModel> Questions { get; set; }

    }

    public class QuestionStudentViewModel
    {
        public int StoryID { get; set; } = 0;
        public string StoryTitle { get; set; } = "";
        public List<QuestionModel> Questions { get; set; }

    }

    public class QuestionModel
    {
        public int ID { get; set; } = 0;

        [AllowHtml]
        public string Question { get; set; } = "";
        public int StoryID { get; set; } = 0;
        public int AddedBy { get; set; } = 0;
        public DateTime DateAdded { get; set; } = new DateTime();
        public int UpdatedBy { get; set; } = 0;
        public DateTime DateUpdated { get; set; } = new DateTime();
        public int DeletedBy { get; set; } = 0;
        public DateTime DateDeleted { get; set; } = new DateTime();
        public bool IsDeleted { get; set; } = false;

        public List<AnswerModel> Answers { get; set; }
    }

    public class AnswerModel
    {
        public int ID { get; set; } = 0;
        public int QuestionID { get; set; } = 0;
        public string Option { get; set; } = "";
        public bool IsCorrect { get; set; } = false;
        public int Sequence { get; set; } = 0;
    }

    public class QuestionAndAnswerModel
    {
        public int StoryID { get; set; } = 0;
        public int QuestionID { get; set; } = 0;
        public string Question { get; set; } = "";
        public string Option1 { get; set; } = "";
        public string Option2 { get; set; } = "";
        public string Option3 { get; set; } = "";
        public string Option4 { get; set; } = "";
        public int CorrectAnswer { get; set; } = 0;
    }

    public class QuestionResultModel
    {
        public int QuestionID { get; set; } = 0;
        public int AnswerID { get; set; } = 0; 
    }

    public class QuestionResultViewModel
    {
        public int StoryID { get; set; } = 0; 
        public int StudentID { get; set; } = 0;
        public List<QuestionResultModel> QuestionResults { get; set; }
    }

    public class GradeModel
    {
        public int StudentID { get; set; } = 0; 
        public int StoryID { get; set; } = 0;
        public int QuestionID { get; set; } = 0;
        public int StudentAnswerID { get; set; } = 0;
        public int Attempt { get; set; } = 0;
    }

    public class QuizAssessmentModel
    {
        public decimal QuizAssessmentPercentage { get; set; } = 0;
        public string QuizAssessmentIcon { get; set; } = "";
        public string QuizAssessmentMessage { get; set; } = "";
    }

    public class AnswerComputeModel
    {
        public int CountCorrectAnswer { get; set; } = 0;
        public int CountTotalQuestion { get; set; } = 0;
    }

    public class StudentSubmissionModel
    {
        public string StoryTitle { get; set; } = "";
        public string StudentName { get; set; } = "";
        public decimal ResultPercentage { get; set; } = 0;
    }

    public class QuestionStudentAnswerViewModel
    {
        public int StoryID { get; set; } = 0;
        public int StudentID { get; set; } = 0; 
        public List<QuestionStudentAnswerModel> QuestionResults { get; set; }
    }

    public class QuestionStudentAnswerModel
    {
        public int QuestionID { get; set; } = 0;
        public string Answer { get; set; } = "";
    }

    public class QuizHistoryViewModel
    {
        public IPagedList<StoryModel> Stories { get; set; }
    }

    public class QuizGradeByStudentViewModel
    {
        public string StoryTitle { get; set; } = "";
        public List<QuizGradeByStudentModel> QuizGradeList { get; set; }
    }

    public class QuizGradeByStudentModel
    {
        public int ID { get; set; } = 0;
        public int StudentID { get; set; } = 0;
        public int StoryID { get; set; } = 0;
        public string StoryTitle { get; set; } = "";
        public int Attempt { get; set; } = 0;
        public int TotalQuestions { get; set; } = 0;
        public int Grade { get; set; } = 0;
        public decimal GradePercentage { get; set; } = 0;
        public string Remarks { get; set; } = "";
        public string CheckedBy { get; set; } = "";
        public DateTime DateChecked { get; set; } = new DateTime();
    }

    public class DetailedQuizGradeByStudentViewModel
    { 
        public int StoryID { get; set; } = 0;
        public string StoryTitle { get; set; } = "";
        public int StudentID { get; set; } = 0;
        public int Attempt { get; set; } = 0;
        public int TotalQuestions { get; set; } = 0;
        public string Remarks { get; set; } = "";
        public List<DetailedQuizGradeByStudentModel> QuizGradeDetailedList { get; set; }
    }

    public class DetailedQuizGradeByStudentModel
    { 
        public int QuestionID { get; set; } = 0;
        public string StoryQuestion { get; set; } = "";
        public int AnswerID { get; set; } = 0;
        public string StudentAnswer { get; set; } = "";
        public int Score { get; set; } = 0;
        public DateTime DateAnswered { get; set; } = new DateTime();
        public int StudentID { get; set; } = 0;
        public int StoryID { get; set; } = 0;
        public string StoryTitle { get; set; } = "";
        public int Attempt { get; set; } = 0;
        public int TotalQuestions { get; set; } = 0;
        public int Grade { get; set; } = 0;
        public decimal GradePercentage { get; set; } = 0;
        public string Remarks { get; set; } = "";
        public string CheckedBy { get; set; } = "";
        public DateTime DateChecked { get; set; } = new DateTime();
        public string MultipleChoiceCorrectAnswer { get; set; } = "";
        public List<AnswerModel> MultipleChoiceAnswers { get; set; }
    }

    public class StudentProgressModel
    {
        public int ID { get; set; } = 0;
        public int StudentID { get; set; } = 0;
        public int StoryID { get; set; } = 0;
        public int LastPageRead { get; set; } = 0;
        public int TotalPages { get; set; } = 0;
        public string Status { get; set; } = "";
        public int StoryCategoryID { get; set; } = 0;
    }

    public class StoryCategoryModel
    {
        public int ID { get; set; } = 0; 
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int Sequence { get; set; } = 0;
        public string Icon { get; set; } = "";
    }

}