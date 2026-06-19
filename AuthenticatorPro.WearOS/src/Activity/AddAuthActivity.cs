// Copyright (C) 2022 jmh
// SPDX-License-Identifier: GPL-3.0-only

using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AuthenticatorPro.Droid.Shared.Query;
using AuthenticatorPro.Shared.Data;
using AuthenticatorPro.Shared.Data.Generator;
using AuthenticatorPro.WearOS.Data;
using System;

namespace AuthenticatorPro.WearOS.Activity
{
    [Activity(Label = "Add Authenticator")]
    internal class AddAuthActivity : AppCompatActivity
    {
        private EditText _issuerText;
        private EditText _usernameText;
        private EditText _secretText;
        private Spinner _typeSpinner;
        private Spinner _algorithmSpinner;
        private EditText _digitsText;
        private EditText _periodText;
        private Button _saveButton;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activityAddAuth);

            _issuerText = FindViewById<EditText>(Resource.Id.textIssuer);
            _usernameText = FindViewById<EditText>(Resource.Id.textUsername);
            _secretText = FindViewById<EditText>(Resource.Id.textSecret);
            _typeSpinner = FindViewById<Spinner>(Resource.Id.spinnerType);
            _algorithmSpinner = FindViewById<Spinner>(Resource.Id.spinnerAlgorithm);
            _digitsText = FindViewById<EditText>(Resource.Id.textDigits);
            _periodText = FindViewById<EditText>(Resource.Id.textPeriod);
            _saveButton = FindViewById<Button>(Resource.Id.buttonSave);

            _typeSpinner.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem,
                new[] { "TOTP", "HOTP", "Steam", "mOTP" });

            _algorithmSpinner.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem,
                new[] { "SHA1", "SHA256", "SHA512" });

            _secretText.InputType = Android.Text.InputTypes.TextVariationVisiblePassword;

            _saveButton.Click += OnSaveClick;
        }

        private async void OnSaveClick(object sender, EventArgs args)
        {
            var issuer = _issuerText.Text?.Trim();
            var secret = _secretText.Text?.Trim();

            if (String.IsNullOrEmpty(issuer))
            {
                Toast.MakeText(this, "Issuer is required", ToastLength.Short).Show();
                return;
            }

            if (String.IsNullOrEmpty(secret))
            {
                Toast.MakeText(this, "Secret is required", ToastLength.Short).Show();
                return;
            }

            var username = _usernameText.Text?.Trim() ?? "";
            var type = (AuthenticatorType)_typeSpinner.SelectedItemPosition;
            var algorithm = _algorithmSpinner.SelectedItemPosition switch
            {
                1 => HashAlgorithm.Sha256,
                2 => HashAlgorithm.Sha512,
                _ => HashAlgorithm.Sha1
            };

            var digits = 6;
            if (int.TryParse(_digitsText.Text, out var parsedDigits) && parsedDigits > 0)
                digits = parsedDigits;

            var period = 30;
            if (int.TryParse(_periodText.Text, out var parsedPeriod) && parsedPeriod > 0)
                period = parsedPeriod;

            var auth = new WearAuthenticator(type, secret.ToUpper().Replace(" ", ""), null,
                issuer, username, period, digits, algorithm, 0, null);

            var db = new LocalAuthDatabase(this);
            await db.Init();
            await db.Add(auth);

            Toast.MakeText(this, "Authenticator added", ToastLength.Short).Show();
            SetResult(Result.Ok);
            Finish();
        }
    }
}