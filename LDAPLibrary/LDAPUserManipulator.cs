﻿using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using LDAPLibrary.Interfarces;

namespace LDAPLibrary
{
    public class LdapUserManipulator : ILdapConnectionObserver
    {
        private readonly ILogger _logger;
        private const string DefaultUserSn = "Default Surname";
        private const string DefaultUserCn = "Default CommonName";
        private LdapConnection _ldapConnection;

        public LdapUserManipulator(ILogger logger)
        {
            _logger = logger;
        }

        public void SetLdapConnection(LdapConnection ldapConnection)
        {
            _ldapConnection = ldapConnection;
        }

        /// <summary>
        ///     Create a new LDAPUser
        /// </summary>
        /// <param name="newUser">User to create</param>
        /// <param name="objectClass"></param>
        /// <returns> Success or Failed</returns>
        public LdapState CreateUser(ILdapUser newUser, string objectClass)
        {
            try
            {
                var addReq = new AddRequest {DistinguishedName = newUser.GetUserDn()};
                addReq.Attributes.Add(new DirectoryAttribute("objectClass", objectClass));
                addReq.Attributes.Add(new DirectoryAttribute("cn", newUser.GetUserCn()));
                addReq.Attributes.Add(new DirectoryAttribute("sn", newUser.GetUserSn()));

                foreach (string attributeName in newUser.GetUserAttributeKeys())
                {
                    foreach (string attributeValue in newUser.GetUserAttribute(attributeName))
                    {
                        addReq.Attributes.Add(new DirectoryAttribute(attributeName, attributeValue));
                    }
                }
                // cast the returned DirectoryResponse as an AddResponse object
                _ldapConnection.SendRequest(addReq);
            }
            catch (Exception e)
            {
                _logger.Write(_logger.BuildLogMessage(e.Message, LdapState.LdapCreateUserError));
                return LdapState.LdapCreateUserError;
            } 
            _logger.Write(_logger.BuildLogMessage("Create User Operation Success", LdapState.LdapCreateUserError));
            return LdapState.LdapUserManipulatorSuccess;
        }

        /// <summary>
        ///     Delete an LDAPUser
        /// </summary>
        /// <param name="user">User to delete</param>
        /// <returns>Success or Failed</returns>
        public LdapState DeleteUser(ILdapUser user)
        {
            try
            {
                //Create the delete request
                var deleteReq = new DeleteRequest(user.GetUserDn());

                //Perform the deletion
                _ldapConnection.SendRequest(deleteReq);
            }
            catch (Exception e)
            {
                _logger.Write(_logger.BuildLogMessage(e.Message, LdapState.LdapDeleteUserError));
                return LdapState.LdapDeleteUserError;
            }
            _logger.Write(_logger.BuildLogMessage("Delete User Operation Success", LdapState.LdapUserManipulatorSuccess));
            return LdapState.LdapUserManipulatorSuccess; 
        }

        /// <summary>
        ///     Modify an LDAPUser Attribute
        /// </summary>
        /// <param name="operationType">Operation to execute on the attribute</param>
        /// <param name="user">LDAPUser's Attribute</param>
        /// <param name="attributeName">Attribute name</param>
        /// <param name="attributeValue">Attribute Value</param>
        /// <returns>Success or Failed</returns>
        public LdapState ModifyUserAttribute(DirectoryAttributeOperation operationType, ILdapUser user, string attributeName,
            string attributeValue)
        {
            try
            {
                //Prepare the modify request
                var modRequest = new ModifyRequest(user.GetUserDn(), operationType, attributeName, attributeValue);

                //Perform the modify
                _ldapConnection.SendRequest(modRequest);
            }
            catch (Exception e)
            {
                _logger.Write(_logger.BuildLogMessage(e.Message, LdapState.LdapModifyUserAttributeError));
                return LdapState.LdapModifyUserAttributeError;
            }

            switch (operationType)
            {
                case DirectoryAttributeOperation.Add:
                    user.InsertUserAttribute(attributeName, attributeValue);
                    break;
                case DirectoryAttributeOperation.Delete:
                    user.DeleteUserAttribute(attributeName, attributeValue);
                    break;
                case DirectoryAttributeOperation.Replace:
                    user.OverwriteUserAttribute(attributeName, attributeValue);
                    break;
            }
            _logger.Write(_logger.BuildLogMessage("Modify User Attribute Operation Success", LdapState.LdapUserManipulatorSuccess));
            return LdapState.LdapUserManipulatorSuccess; 
        }

        /// <summary>
        ///     Change an LDAPUser's Password
        /// </summary>
        /// <param name="user">LDAPUser to change the password</param>
        /// <param name="newPwd">New Passowrd</param>
        /// <returns>Success or Failed</returns>
        public LdapState ChangeUserPassword(ILdapUser user, string newPwd)
        {
            try
            {
                var modifyUserPassword = new DirectoryAttributeModification
                {
                    Operation = DirectoryAttributeOperation.Replace,
                    Name = "userPassword"
                };
                modifyUserPassword.Add(newPwd);

                var modifyRequest = new ModifyRequest(user.GetUserDn(), modifyUserPassword);

                _ldapConnection.SendRequest(modifyRequest);
            }
            catch (Exception e)
            {
                _logger.Write(_logger.BuildLogMessage(e.Message, LdapState.LdapChangeUserPasswordError));
                return LdapState.LdapChangeUserPasswordError;
            }

            user.OverwriteUserAttribute("userPassword", newPwd);

            _logger.Write(_logger.BuildLogMessage("Change Password Operation Success", LdapState.LdapUserManipulatorSuccess));
            return LdapState.LdapUserManipulatorSuccess; 
        }

        /// <summary>
        ///     Search Users in the LDAP system
        /// </summary>
        /// <param name="baseDn">Starting DN of the search</param>
        /// <param name="matchFieldUsername"></param>
        /// <param name="otherReturnedAttributes">Addictional attributes added to the results LDAPUsers objects</param>
        /// <param name="searchedUsers">Credential for the search</param>
        /// <param name="searchResult">LDAPUsers object returned in the search</param>
        /// <param name="userObjectClass"></param>
        /// <returns>Boolean that comunicate the result of search</returns>
        public LdapState SearchUsers(string baseDn, string userObjectClass, string matchFieldUsername,
            List<string> otherReturnedAttributes, string[] searchedUsers, out List<ILdapUser> searchResult)
        {
            searchResult = new List<ILdapUser>();

            if (otherReturnedAttributes == null)
                otherReturnedAttributes = new List<string>();

            //Add required search return attributes to the list

            if (!otherReturnedAttributes.Contains("cn")) otherReturnedAttributes.Add("cn");
            if (!otherReturnedAttributes.Contains("sn")) otherReturnedAttributes.Add("sn");

            try
            {
                //Foreach all the credential,for everyone do the search and add user results to the out parameter
                foreach (string users in searchedUsers)
                {
                    //Create the filter for the search
                    string ldapSearchFilter = String.Format("(&(objectClass={0})({1}={2}))", userObjectClass,
                        matchFieldUsername, users);

                    //Componing search request
                    var search = new SearchRequest(baseDn, ldapSearchFilter, SearchScope.Subtree,
                        otherReturnedAttributes.ToArray());

                    //Perforing the search
                    var searchReturn = (SearchResponse) _ldapConnection.SendRequest(search);

                    //For all the searchReturn we create a new LDAPUser obj and add that to the return searchResult
                    if (searchReturn != null)
                        foreach (SearchResultEntry userReturn in searchReturn.Entries)
                        {
                            //Required attributes inizialization
                            string tempUserDn = userReturn.DistinguishedName;
                            string tempUserCn = DefaultUserCn;
                            string tempUserSn = DefaultUserSn;
                            var tempUserOtherAttributes = new Dictionary<string, List<string>>();

                            //Cycle attributes
                            if (userReturn.Attributes.Values != null)
                                foreach (DirectoryAttribute userReturnAttribute in userReturn.Attributes.Values)
                                {
                                    //if is CN or SN, set right String else add attribute to dictionary
                                    if (userReturnAttribute.Name.Equals("cn") || userReturnAttribute.Name.Equals("CN"))
                                    {
                                        object[] values = userReturnAttribute.GetValues(Type.GetType("System.String"));
                                        tempUserCn = (string) values[0];
                                    }
                                    else if (userReturnAttribute.Name.Equals("sn") ||
                                             userReturnAttribute.Name.Equals("SN"))
                                    {
                                        object[] values =
                                            userReturnAttribute.GetValues(Type.GetType("System.String"));
                                        tempUserSn = (string) values[0];
                                    }
                                    else
                                    {
                                        object[] values =
                                            userReturnAttribute.GetValues(Type.GetType("System.String"));

                                        var stringValues =
                                            new List<string>(Array.ConvertAll(values, Convert.ToString));
                                        tempUserOtherAttributes.Add(userReturnAttribute.Name, stringValues);
                                    }
                                }
                            //Create new tempUser and add to the searchResult
                            var tempUser = new LdapUser(tempUserDn, tempUserCn, tempUserSn, tempUserOtherAttributes);
                            searchResult.Add(tempUser);
                        }
                }
            }
            catch (Exception e)
            {
                _logger.Write(_logger.BuildLogMessage(e.Message, LdapState.LdapSearchUserError));
                return LdapState.LdapSearchUserError;
            }

            if (searchResult.Count == 0)
            {
                _logger.Write(_logger.BuildLogMessage("Search Operation with NO RESULTS", LdapState.LdapSearchUserError));
                return LdapState.LdapSearchUserError;
            }
            _logger.Write(_logger.BuildLogMessage("Search Operation Success", LdapState.LdapUserManipulatorSuccess));
            return LdapState.LdapUserManipulatorSuccess;
        }
    }
}