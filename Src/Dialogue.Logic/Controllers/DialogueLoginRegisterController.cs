﻿using System;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Dialogue.Logic.Application;
using Dialogue.Logic.Constants;
using Dialogue.Logic.Mapping;
using Dialogue.Logic.Models;
using Dialogue.Logic.Models.ViewModels;
using Dialogue.Logic.Services;
using Umbraco.Core.Models;
using Umbraco.Web.Models;
using Member = Dialogue.Logic.Models.Member;
using System.Linq;
using System.Collections.Generic;
using Dialogue.Logic.Helpers;

namespace Dialogue.Logic.Controllers
{
    #region Render Controllers
    public partial class DialogueLoginController : BaseRenderController
    {

        [ModelStateToTempData]
        public override ActionResult Index(RenderModel model)
        {
            // Create the empty view model
            var pageModel = new LogOnModel(model.Content);

            DialogueMapper.PopulateCommonUmbracoProperties(pageModel, model.Content);

            // See if a return url is present or not and add it
            var forgotPassword = Request["forgot"];
            var resetPassword = Request["resettoken"];
            if (!string.IsNullOrEmpty(forgotPassword))
            {
                pageModel.ShowForgotPassword = true;
            }
            else if (!string.IsNullOrEmpty(resetPassword))
            {
                pageModel.ShowResetPassword = true;
            }

            // Return the model to the current template
            return View(PathHelper.GetThemeViewPath("Login"), pageModel);
        }

    }

    public partial class DialogueRegisterController : BaseRenderController
    {

        [ModelStateToTempData]
        public override ActionResult Index(RenderModel model)
        {
            // Create the empty view model
            var pageModel = new Models.RegisterModel(model.Content);

            DialogueMapper.PopulateCommonUmbracoProperties(pageModel, model.Content);

            // Return the model to the current template
            return View(PathHelper.GetThemeViewPath("Register"), pageModel);
        }

    }

    #endregion

    #region SurefaceController

    public partial class DialogueLoginRegisterSurfaceController : BaseSurfaceController
    {
        [ChildActionOnly]
        public ActionResult LogOnForm()
        {
            var viewModel = new LogOnViewModel();
            // See if a return url is present or not and add it
            var returnUrl = Request["ReturnUrl"];
            if (!string.IsNullOrEmpty(returnUrl))
            {
                viewModel.ReturnUrl = returnUrl;
            }
            return View(PathHelper.GetThemePartialViewPath("LogOnForm"), viewModel);
        }

        [ChildActionOnly]
        public ActionResult RegisterForm()
        {
            var viewModel = new RegisterViewModel();

            // See if a return url is present or not and add it
            var returnUrl = Request["ReturnUrl"];
            if (!string.IsNullOrEmpty(returnUrl))
            {
                viewModel.ReturnUrl = returnUrl;
            }

            return View(PathHelper.GetThemePartialViewPath("RegisterForm"), viewModel);
        }

        [ChildActionOnly]
        public ActionResult ForgotPasswordForm()
        {
            var viewModel = new ForgotPasswordViewModel();
            return View(PathHelper.GetThemePartialViewPath("ForgotPasswordForm"), viewModel);
        }


        /// <summary>
        /// Log on post
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ModelStateToTempData]
        public ActionResult LogOn(LogOnViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {                   
                    var message = new GenericMessageViewModel();
                    var user = ServiceFactory.MemberService.Get(model.UserName);
                    if (ServiceFactory.MemberService.Login(model.UserName, model.Password))
                    {
                        // Set last login date
                        if (user.IsApproved && !user.IsLockedOut)
                        {
                            FormsAuthentication.SetAuthCookie(model.UserName, model.RememberMe);

                            if (Url.IsLocalUrl(model.ReturnUrl) && model.ReturnUrl.Length >= 1 && model.ReturnUrl.StartsWith("/")
                                && !model.ReturnUrl.StartsWith("//") && !model.ReturnUrl.StartsWith("/\\"))
                            {
                                return Redirect(model.ReturnUrl);
                            }

                            message.Message = Lang("Members.NowLoggedIn");
                            message.MessageType = GenericMessages.Success;

                            return Redirect("~");
                        }
                    }

                    // Only show if we have something to actually show to the user
                    if (!string.IsNullOrEmpty(message.Message))
                    {
                        ShowMessage(message);
                    }
                    else
                    {
                        if (user == null)
                        {
                            ModelState.AddModelError(string.Empty, Lang("Members.Errors.LogonGeneric"));
                        }
                        else if (!user.IsApproved)
                        {
                            this.ShowNotApprovedMessage(user.Id);
                        }
                        else if (user.IsLockedOut)
                        {
                            ModelState.AddModelError(string.Empty, Lang("Members.Errors.LockedOut"));
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, Lang("Members.Errors.LogonGeneric"));
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, Lang("Members.Errors.LogonGeneric"));
                }
            }
            catch (Exception ex)
            {
                LogError("Error when user logging in", ex);
            }

            // Hack to show form validation
            //ShowModelErrors();

            return CurrentUmbracoPage();
        }

        public void ShowNotApprovedMessage(int userId)
        {
            GenericMessageViewModel message = new GenericMessageViewModel();
            StringBuilder sb = new StringBuilder();
            sb.Append(Lang("Members.Errors.NotApproved") + " ");
            sb.Append("<a href='" + Urls.GenerateUrl(Urls.UrlType.ResendEmailConfirmation) + "?id=" + userId + "'>" + Lang("Members.MemberEmailAuthorisationResend") + "</a>");
            var fullMessage = sb.ToString();
            if (!string.IsNullOrEmpty(fullMessage))
            {
                message.Message = string.Concat("<ul>", fullMessage, "</ul>");
                message.MessageType = GenericMessages.Info;
                ShowMessage(message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ModelStateToTempData]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            Member currentUser = new Member();
            string token = string.Empty;
            try
            {
                if (ModelState.IsValid)
                {
                    currentUser = ServiceFactory.MemberService.GetByEmail(model.EmailAddress);
                    if (currentUser != null)
                    {
                        token = ServiceFactory.MemberService.ResetPasswordWithToken(currentUser.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("Error resetting password for {0}", model.EmailAddress), ex);
            }

            if (!string.IsNullOrEmpty(token))
            {
                var sb = new StringBuilder();
                sb.AppendFormat("<p>{0}</p>", string.Format(Lang("Members.ForgotPassword.Email"), Settings.ForumName));
                string url = string.Format("http://{0}{1}{2}{3}", HttpContext.Request.Url.Authority, Settings.LoginUrl, "?resettoken=", token);
                sb.Append(string.Format("<p><a href='{0}'>{0}</b></p>", url, Lang("Members.ResetPassword.Link")));
                var email = new Email
                {
                    EmailFrom = Settings.NotificationReplyEmailAddress,
                    EmailTo = currentUser.Email,
                    NameTo = currentUser.UserName,
                    Subject = Lang("Members.ForgotPassword.Subject")
                };
                email.Body = ServiceFactory.EmailService.EmailTemplate(email.NameTo, sb.ToString());
                ServiceFactory.EmailService.SendMail(email);

                // We use temp data because we are doing a redirect
                ShowMessage(new GenericMessageViewModel
                {
                    Message = Lang("Members.ForgotPassword.SuccessMessage"),
                    MessageType = GenericMessages.Success
                });
                return Redirect(Settings.LoginUrl);
            }

            ModelState.AddModelError("", Lang("Members.ForgotPassword.ErrorMessage"));
            
            // Hack to show form validation
            ShowModelErrors();
            return CurrentUmbracoPage();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ModelStateToTempData]
        public ActionResult Register(RegisterViewModel userModel)
        {
            if (Settings.SuspendRegistration != true)
            {
                if (!AppHelpers.IsValidEmail(userModel.Email))
                {
                    return CurrentUmbracoPage();
                }

                // If we're using Google reCAPTCHA, validate that
                if (Settings.UseGoogleRecaptcha)
                {
                    string encodedResponse = Request.Form["g-Recaptcha-Response"];
                    if (!ReCaptchaHelper.Validate(encodedResponse, Settings.GoogleRecaptchaSecretKey))
                    {
                        // Error validating repcatcha
                        ModelState.AddModelError(string.Empty, Lang("Error.InvalidGoogleRecaptchaResponse"));
                        return CurrentUmbracoPage();
                    }
                }

                // See if there is a spam question and if so, the answer matches
                else if (!string.IsNullOrEmpty(Settings.SpamQuestion))
                {
                    // There is a spam question, if answer is wrong return with error
                    if (userModel.SpamAnswer == null || userModel.SpamAnswer.ToLower().Trim() != Settings.SpamAnswer.ToLower())
                    {
                        // POTENTIAL SPAMMER!
                        ModelState.AddModelError(string.Empty, Lang("Error.WrongAnswerRegistration"));
                        //ShowModelErrors();
                        return CurrentUmbracoPage();
                    }
                }

                // Standard Login
                userModel.LoginType = LoginType.Standard;

                // Do the register logic
                return MemberRegisterLogic(userModel);

            }

            return CurrentUmbracoPage();
        }

        public ActionResult MemberRegisterLogic(RegisterViewModel userModel)
        {
            var forumReturnUrl = Settings.ForumRootUrl;
            var newMemberGroup = Settings.Group;
            if (userModel.ForumId != null && userModel.ForumId != Settings.ForumId)
            {
                var correctForum = Dialogue.Settings((int)userModel.ForumId);
                forumReturnUrl = correctForum.ForumRootUrl;
                newMemberGroup = correctForum.Group;
            }

            using (var unitOfWork = UnitOfWorkManager.NewUnitOfWork())
            {
                // Secondly see if the email is banned
                if (ServiceFactory.BannedEmailService.EmailIsBanned(userModel.Email))
                {
                    ModelState.AddModelError(string.Empty, Lang("Error.EmailIsBanned"));

                    if (userModel.LoginType != LoginType.Standard)
                    {
                        ShowModelErrors();
                        return Redirect(Settings.RegisterUrl);
                    }
                    return CurrentUmbracoPage();
                }

                var userToSave = AppHelpers.UmbMemberHelper().CreateRegistrationModel(AppConstants.MemberTypeAlias);
                userToSave.Username = ServiceFactory.BannedWordService.SanitiseBannedWords(userModel.UserName);
                userToSave.Name = userToSave.Username;
                userToSave.UsernameIsEmail = false;
                userToSave.Email = userModel.Email;
                userToSave.Password = userModel.Password;

                var homeRedirect = false;

                MembershipCreateStatus createStatus;
                AppHelpers.UmbMemberHelper().RegisterMember(userToSave, out createStatus, false);

                if (createStatus != MembershipCreateStatus.Success)
                {
                    ModelState.AddModelError(string.Empty, ServiceFactory.MemberService.ErrorCodeToString(createStatus));
                }
                else
                {
                    // Get the umbraco member
                    var umbracoMember = AppHelpers.UmbServices().MemberService.GetByUsername(userToSave.Username);

                    // Set the role/group they should be in
                    AppHelpers.UmbServices().MemberService.AssignRole(umbracoMember.Id, newMemberGroup.Name);

                    // See if this is a social login and we have their profile pic
                    if (!string.IsNullOrEmpty(userModel.SocialProfileImageUrl))
                    {
                        // We have an image url - Need to save it to their profile
                        var image = AppHelpers.GetImageFromExternalUrl(userModel.SocialProfileImageUrl);

                        // Upload folder path for member
                        var uploadFolderPath = AppHelpers.GetMemberUploadPath(umbracoMember.Id);

                        // Upload the file
                        var uploadResult = AppHelpers.UploadFile(image, uploadFolderPath);

                        // Don't throw error if problem saving avatar, just don't save it.
                        if (uploadResult.UploadSuccessful)
                        {                            
                            umbracoMember.Properties[AppConstants.PropMemberAvatar].Value = string.Concat(VirtualPathUtility.ToAbsolute(AppConstants.UploadFolderPath), umbracoMember.Id, "/", uploadResult.UploadedFileName);
                        }
                    }

                    // Now check settings, see if users need to be manually authorised
                    // OR Does the user need to confirm their email
                    var manuallyAuthoriseMembers = Settings.ManuallyAuthoriseNewMembers;
                    var memberEmailAuthorisationNeeded = Settings.NewMembersMustConfirmAccountsViaEmail;
                    if (manuallyAuthoriseMembers || memberEmailAuthorisationNeeded)
                    {
                        umbracoMember.IsApproved = false;
                    }

                    // Store access token for social media account in case we want to do anything with it
                    if (userModel.LoginType == LoginType.Facebook)
                    {
                        umbracoMember.Properties[AppConstants.PropMemberFacebookAccessToken].Value = userModel.UserAccessToken;    
                    }
                    if (userModel.LoginType == LoginType.Google)
                    {
                        umbracoMember.Properties[AppConstants.PropMemberGoogleAccessToken].Value = userModel.UserAccessToken;
                    }

                    // Do a save on the member
                    AppHelpers.UmbServices().MemberService.Save(umbracoMember);

                    if (Settings.EmailAdminOnNewMemberSignup)
                    {
                        var sb = new StringBuilder();
                        sb.AppendFormat("<p>{0}</p>", string.Format(Lang("Members.NewMemberRegistered"), Settings.ForumName, Settings.ForumRootUrl));
                        sb.AppendFormat("<p>{0} - {1}</p>", userToSave.Username, userToSave.Email);
                        var email = new Email
                        {
                            EmailTo = Settings.AdminEmailAddress,
                            EmailFrom = Settings.NotificationReplyEmailAddress,
                            NameTo = Lang("Members.Admin"),
                            Subject = Lang("Members.NewMemberSubject")
                        };
                        email.Body = ServiceFactory.EmailService.EmailTemplate(email.NameTo, sb.ToString());
                        ServiceFactory.EmailService.SendMail(email);
                    }

                    // Fire the activity Service
                    ServiceFactory.ActivityService.MemberJoined(MemberMapper.MapMember(umbracoMember));

                    var userMessage = new GenericMessageViewModel();

                    // Set the view bag message here
                    if (manuallyAuthoriseMembers)
                    {
                        userMessage.Message = Lang("Members.NowRegisteredNeedApproval");
                        userMessage.MessageType = GenericMessages.Success;
                    }
                    else if (memberEmailAuthorisationNeeded)
                    {
                        userMessage.Message = Lang("Members.MemberEmailAuthorisationNeeded");
                        userMessage.MessageType = GenericMessages.Success;
                    }
                    else
                    {
                        // If not manually authorise then log the user in
                        FormsAuthentication.SetAuthCookie(userToSave.Username, true);
                        userMessage.Message = Lang("Members.NowRegistered");
                        userMessage.MessageType = GenericMessages.Success;
                    }

                    //Show the message
                    ShowMessage(userMessage);

                    if (!manuallyAuthoriseMembers && !memberEmailAuthorisationNeeded)
                    {
                        homeRedirect = true;
                    }

                    try
                    {
                        unitOfWork.Commit();

                        // Only send the email if the admin is not manually authorising emails or it's pointless                        
                        ServiceFactory.MemberService.SendEmailConfirmationEmail(umbracoMember);

                        if (homeRedirect && !string.IsNullOrEmpty(forumReturnUrl))
                        {
                            if (Url.IsLocalUrl(userModel.ReturnUrl) && userModel.ReturnUrl.Length > 1 && userModel.ReturnUrl.StartsWith("/")
                            && !userModel.ReturnUrl.StartsWith("//") && !userModel.ReturnUrl.StartsWith("/\\"))
                            {
                                return Redirect(userModel.ReturnUrl);
                            }
                            return Redirect(forumReturnUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        unitOfWork.Rollback();
                        LogError("Eror during member registering", ex);
                        FormsAuthentication.SignOut();
                        ModelState.AddModelError(string.Empty, ex.Message);
                    }
                }
                if (userModel.LoginType != LoginType.Standard)
                {
                    ShowModelErrors();
                    return Redirect(Settings.RegisterUrl);
                }

                return Redirect(Settings.LoginUrl);
            }
        }

        /// <summary>
        /// Email confirmation page from the link in the users email
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult EmailConfirmation(int id)
        {
            using (var unitOfWork = UnitOfWorkManager.NewUnitOfWork())
            {
                // Checkconfirmation
                var user = ServiceFactory.MemberService.Get(id);
                if (user != null)
                {
                    // Set the user to active
                    user.IsApproved = true;

                    // Delete Cookie and log them in if this cookie is present
                    if (Request.Cookies[AppConstants.MemberEmailConfirmationCookieName] != null)
                    {
                        var myCookie = new HttpCookie(AppConstants.MemberEmailConfirmationCookieName)
                        {
                            Expires = DateTime.Now.AddDays(-1)
                        };
                        Response.Cookies.Add(myCookie);

                        // Login code
                        FormsAuthentication.SetAuthCookie(user.UserName, false);
                    }

                    // Show a new message
                    // We use temp data because we are doing a redirect
                    ShowMessage(new GenericMessageViewModel
                    {
                        Message = Lang("Members.NowApproved"),
                        MessageType = GenericMessages.Success
                    });
                }

                try
                {
                    unitOfWork.Commit();
                }
                catch (Exception ex)
                {
                    unitOfWork.Rollback();
                    LogError(ex);
                }
            }

            return RedirectToUmbracoPage(Settings.ForumId);
        }

        [ChildActionOnly]
        public ActionResult ResetPasswordForm()
        {
            return PartialView(PathHelper.GetThemePartialViewPath("ResetPasswordForm"), new ResetPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HandleResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                List<ModelErrorCollection> errors = ModelState.Select(x => x.Value.Errors).Where(y => y.Count > 0).ToList();
                foreach (ModelErrorCollection error in errors)
                {
                    IEnumerable<ModelError> modelStateErrors = this.ModelState.Keys.SelectMany(key => this.ModelState[key].Errors);
                    string errorMessages = string.Join(", ", modelStateErrors.Select(x => x.ErrorMessage).ToList());
                    ModelState.AddModelError(string.Empty, errorMessages);
                }

                return CurrentUmbracoPage();
            }

            bool success = false;

            // Get member from email
            var resetMember = ServiceFactory.MemberService.GetByEmail(model.EmailAddress);

            // Ensure we have that member
            if (resetMember != null)
            {
                // Get the querystring GUID
                var resetToken = Request.QueryString["resetToken"];

                // Ensure we have a reset token value
                if (!string.IsNullOrEmpty(resetToken))
                {
                    success = ServiceFactory.MemberService.ProcessResetPasswordWithToken(resetMember.Id, resetToken, model.NewPassword);
                }
            }

            if (success)
            {
                ShowMessage(new GenericMessageViewModel
                {
                    Message = Lang("Members.ChangePassword.Success"),
                    MessageType = GenericMessages.Success
                });
            }
            else
            {
                ModelState.AddModelError(string.Empty, Lang("Members.Errors.ResetPasswordInvalid"));
                ShowModelErrors();
            }

            return Redirect(Settings.LoginUrl);
        }
    }

    #endregion
}