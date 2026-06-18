// Copyright (C) 2022 jmh
// SPDX-License-Identifier: GPL-3.0-only

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Wear.Widget;
using AuthenticatorPro.Droid.Shared.Data;
using AuthenticatorPro.Droid.Shared.Query;
using AuthenticatorPro.Droid.Shared.Util;
using AuthenticatorPro.Shared.Data;
using AuthenticatorPro.Shared.Data.Generator;
using AuthenticatorPro.Shared.Entity;
using AuthenticatorPro.WearOS.Cache;
using AuthenticatorPro.WearOS.Data;
using AuthenticatorPro.WearOS.List;
using AuthenticatorPro.WearOS.Util;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthenticatorPro.WearOS.Activity
{
    [Activity(Label = "@string/displayName", MainLauncher = true, Icon = "@mipmap/ic_launcher",
        Theme = "@style/AppTheme")]
    internal class MainActivity : AppCompatActivity
    {
        private const string AuthenticatorCacheName = "authenticators";

        private LinearLayout _emptyLayout;
        private CircularProgressLayout _circularProgressLayout;
        private WearableRecyclerView _authList;
        private FloatingActionButton _fabAdd;

        private AuthenticatorView _authView;
        private ListCache<WearAuthenticator> _authCache;
        private CustomIconCache _customIconCache;
        private LocalAuthDatabase _localDb;
        private PreferenceWrapper _preferences;
        private bool _justLaunched;

        private AuthenticatorListAdapter _authListAdapter;

        private const int RequestCodeAddAuth = 1001;

        public MainActivity()
        {
            _justLaunched = true;
        }

        protected override async void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.activityMain);

            _preferences = new PreferenceWrapper(this);

            _localDb = new LocalAuthDatabase(this);
            await _localDb.Init();

            _authCache = new ListCache<WearAuthenticator>(AuthenticatorCacheName, this);
            _customIconCache = new CustomIconCache(this);

            await _authCache.Init();
            await _customIconCache.Add("", Array.Empty<byte>());

            var authenticators = _localDb.GetAll();
            await _authCache.Replace(authenticators);

            var defaultCategory = _preferences.DefaultCategory;
            _authView = new AuthenticatorView(_authCache, defaultCategory, _preferences.SortMode);

            RunOnUiThread(delegate
            {
                InitViews();

                if (!_authCache.GetItems().Any())
                {
                    return;
                }

                AnimUtil.FadeOutView(_circularProgressLayout, AnimUtil.LengthShort, false, delegate
                {
                    CheckEmptyState();
                });
            });
        }

        protected override async void OnResume()
        {
            base.OnResume();

            if (!_justLaunched)
            {
                await ReloadData();
            }

            if (_justLaunched)
            {
                _justLaunched = false;
                var defaultAuth = _preferences.DefaultAuth;

                if (defaultAuth != null)
                {
                    var authPosition = _authView.FindIndex(a => a.Secret.GetHashCode() == defaultAuth);

                    if (authPosition >= 0)
                        OnItemClicked(null, authPosition);
                }
            }

            RunOnUiThread(delegate
            {
                AnimUtil.FadeOutView(_circularProgressLayout, AnimUtil.LengthShort, false, delegate
                {
                    CheckEmptyState();
                });
            });
        }

        private async Task ReloadData()
        {
            await _localDb.Init();
            var items = _localDb.GetAll();

            if (_authCache.Dirty(items, new WearAuthenticatorComparer()))
            {
                await _authCache.Replace(items);
                _authView.Update();
                RunOnUiThread(_authListAdapter.NotifyDataSetChanged);
            }
        }

        private void InitViews()
        {
            _circularProgressLayout = FindViewById<CircularProgressLayout>(Resource.Id.layoutCircularProgress);
            _emptyLayout = FindViewById<LinearLayout>(Resource.Id.layoutEmpty);
            _authList = FindViewById<WearableRecyclerView>(Resource.Id.list);
            _fabAdd = FindViewById<FloatingActionButton>(Resource.Id.fabAdd);

            _authList.EdgeItemsCenteringEnabled = true;
            _authList.HasFixedSize = true;
            _authList.SetItemViewCacheSize(12);
            _authList.SetItemAnimator(null);

            var layoutCallback = new AuthenticatorListLayoutCallback(this);
            _authList.SetLayoutManager(new WearableLinearLayoutManager(this, layoutCallback));

            _authListAdapter = new AuthenticatorListAdapter(_authView, _customIconCache);
            _authListAdapter.ItemClicked += OnItemClicked;
            _authListAdapter.ItemLongClicked += OnItemLongClicked;
            _authListAdapter.HasStableIds = true;
            _authListAdapter.DefaultAuth = _preferences.DefaultAuth;
            _authList.SetAdapter(_authListAdapter);

            _fabAdd.Click += async (s, e) =>
            {
                var intent = new Intent(this, typeof(AddAuthActivity));
                StartActivityForResult(intent, RequestCodeAddAuth);
            };
        }

        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == RequestCodeAddAuth && resultCode == Result.Ok)
            {
                await ReloadData();
                RunOnUiThread(delegate
                {
                    AnimUtil.FadeOutView(_circularProgressLayout, AnimUtil.LengthShort, false, CheckEmptyState);
                });
            }
        }

        private void CheckEmptyState()
        {
            if (!_authView.Any())
            {
                _emptyLayout.Visibility = ViewStates.Visible;
                _authList.Visibility = ViewStates.Invisible;
            }
            else
            {
                _emptyLayout.Visibility = ViewStates.Invisible;
                _authList.Visibility = ViewStates.Visible;
                _authList.RequestFocus();
            }
        }

        private async void OnItemClicked(object sender, int position)
        {
            var item = _authView[position];

            if (item == null)
                return;

            if (item.Type.GetGenerationMethod() == GenerationMethod.Counter)
            {
                Toast.MakeText(this, Resource.String.hotpNotSupported, ToastLength.Short).Show();
                return;
            }

            var intent = new Intent(this, typeof(CodeActivity));
            var bundle = new Bundle();

            bundle.PutInt("type", (int)item.Type);
            bundle.PutString("issuer", item.Issuer);
            bundle.PutString("username", item.Username);
            bundle.PutInt("period", item.Period);
            bundle.PutInt("digits", item.Digits);
            bundle.PutString("secret", item.Secret);
            bundle.PutInt("algorithm", (int)item.Algorithm);

            var hasCustomIcon = !String.IsNullOrEmpty(item.Icon) && item.Icon.StartsWith(CustomIconCache.Prefix);
            bundle.PutBoolean("hasCustomIcon", hasCustomIcon);

            if (hasCustomIcon)
            {
                var id = item.Icon[1..];
                var bitmap = await _customIconCache.GetBitmap(id);
                bundle.PutParcelable("icon", bitmap);
            }
            else
            {
                bundle.PutString("icon", item.Icon);
            }

            intent.PutExtras(bundle);
            StartActivity(intent);
        }

        private void OnItemLongClicked(object sender, int position)
        {
            var item = _authView[position];

            if (item == null)
                return;

            var builder = new AlertDialog.Builder(this);
            builder.SetTitle(item.Issuer);
            builder.SetItems(new Java.Lang.String[]
            {
                new Java.Lang.String("Set as default"),
                new Java.Lang.String("Delete")
            }, (s, args) =>
            {
                switch (args.Which)
                {
                    case 0:
                        var oldDefault = _preferences.DefaultAuth;
                        var newDefault = item.Secret.GetHashCode();

                        if (oldDefault == newDefault)
                        {
                            _authListAdapter.DefaultAuth = _preferences.DefaultAuth = null;
                        }
                        else
                        {
                            _authListAdapter.DefaultAuth = _preferences.DefaultAuth = newDefault;
                            _authListAdapter.NotifyItemChanged(position);
                        }

                        if (oldDefault != null)
                        {
                            var oldPos = _authView.FindIndex(a => a.Secret.GetHashCode() == oldDefault);

                            if (oldPos > -1)
                                _authListAdapter.NotifyItemChanged(oldPos);
                        }

                        break;

                    case 1:
                        DeleteAuthenticator(position);
                        break;
                }
            });

            builder.Show();
        }

        private async void DeleteAuthenticator(int position)
        {
            if (position < 0 || position >= _authCache.Count)
                return;

            var auth = _authView[position];
            var secretHash = auth?.Secret.GetHashCode();

            var dbPosition = _localDb.FindIndex(a => a.Secret.GetHashCode() == secretHash);
            if (dbPosition >= 0)
            {
                await _localDb.RemoveAt(dbPosition);
            }

            if (secretHash != null && _preferences.DefaultAuth == secretHash)
            {
                _authListAdapter.DefaultAuth = _preferences.DefaultAuth = null;
            }

            await ReloadData();

            RunOnUiThread(delegate
            {
                _authListAdapter.NotifyDataSetChanged();
                CheckEmptyState();
            });
        }
    }
}