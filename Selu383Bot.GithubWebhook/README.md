You will need to setup some environment variables in order to have this run locally:
* **GithubAuthToken** - the auth token for the user who is going to manage the repositories for students
* **githubclientId** - the oauth client id
* **githubredirect** - the oauth redirect
* **githubsecret** - the oauth client secret
* **HmacSecret** - 32 byte hmac secret key for redirect state verification
* **SecretBoxKey** - libsodium SecretBox key hex encoded, for cookie based session management
* **WebhookSecret** - the github webhook secret for verifying webhook values
* **AzureServicePrincipalData** - the json service principal data for authentication into the subscription. Needs owner over the subscription
