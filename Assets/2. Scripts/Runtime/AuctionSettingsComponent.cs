using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CantResell
{
    public sealed class AuctionSettingsComponent : MonoBehaviour
    {
        public enum VolumeChannel { Master, Music, Effects }
        [SerializeField, Range(0, 1)] private float defaultMasterVolume = 0.8f; //처음 실행할 때 전체 음량
        private string preferencePrefix = "CantResell."; //저장 키의 공통 접두사
        private Vector2Int previousResolution; //미리보기 전 화면 크기
        private FullScreenMode previousMode; //미리보기 전 창 모드
        private double displayDeadline; //해상도 확인을 기다리는 종료 시각
        private int previewFrame; //화면 적용 이전 프레임의 중복 확인 방지
        public float masterVolume { get; private set; } //전체 음량 설정
        public float musicVolume { get; private set; } //음악 음량 설정
        public float effectsVolume { get; private set; } //효과음 음량 설정
        public Vector2Int[] resolutions { get; private set; } = Array.Empty<Vector2Int>(); //선택 가능한 중복 없는 화면 크기
        public Vector2Int selectedResolution { get; private set; } //현재 적용 또는 미리보기 중인 화면 크기
        public bool fullscreen { get; private set; } //전체 화면 선택 상태
        public bool displayPending { get; private set; } //해상도 변경 확인 대기 여부
        public int displaySeconds => Mathf.Max(0, Mathf.CeilToInt((float)(displayDeadline - Time.realtimeSinceStartupAsDouble))); //원복까지 남은 초
        public string displayMessage { get; private set; } = ""; //화면 변경 결과 안내

        public void initialize(string prefix = "CantResell.", bool restoreDisplay = true) //기존 음량과 저장한 화면 설정 복원
        {
            preferencePrefix = prefix;
            masterVolume = normalizeVolume(PlayerPrefs.GetFloat(prefix + "Volume", defaultMasterVolume));
            musicVolume = normalizeVolume(PlayerPrefs.GetFloat(prefix + "MusicVolume", 1));
            effectsVolume = normalizeVolume(PlayerPrefs.GetFloat(prefix + "EffectsVolume", 1));
            Vector2Int current = new Vector2Int(Screen.width, Screen.height); //현재 창의 화면 크기
            Vector2Int desktop = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height); //현재 모니터의 화면 크기
            resolutions = buildResolutions(Screen.resolutions, current, desktop);
            selectedResolution = resolutions.Contains(current) ? current : resolutions[0];
            fullscreen = Screen.fullScreen;
            Vector2Int saved = new Vector2Int(PlayerPrefs.GetInt(prefix + "Width", 0), PlayerPrefs.GetInt(prefix + "Height", 0)); //확정하여 저장한 화면 크기
            if (restoreDisplay && !Application.isEditor && resolutions.Contains(saved))
            {
                selectedResolution = saved;
                fullscreen = PlayerPrefs.GetInt(prefix + "Fullscreen", fullscreen ? 1 : 0) != 0;
                Screen.SetResolution(saved.x, saved.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
        }

        public static float normalizeVolume(float value) //잘못된 설정 값을 유효한 음량으로 제한
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp01(value);
        }

        public static Vector2Int[] buildResolutions(Resolution[] available, Vector2Int current, Vector2Int desktop) //모니터 해상도와 창 모드 크기의 중복 제거
        {
            int maximumWidth = Math.Max(desktop.x, current.x); //선택할 수 있는 최대 가로 크기
            int maximumHeight = Math.Max(desktop.y, current.y); //선택할 수 있는 최대 세로 크기
            if (maximumWidth < 640 || maximumHeight < 360)
            {
                maximumWidth = 1920;
                maximumHeight = 1080;
            }
            List<Vector2Int> sizes = (available ?? Array.Empty<Resolution>()).Select(size => new Vector2Int(size.width, size.height)).ToList(); //OS가 제공한 화면 크기
            sizes.AddRange(new[] { current, desktop, new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) });
            Vector2Int[] result = sizes.Where(size => size.x >= 640 && size.y >= 360 && size.x <= maximumWidth && size.y <= maximumHeight)
                .Distinct().OrderByDescending(size => (long)size.x * size.y).ThenByDescending(size => size.x).ToArray(); //모니터 안에 들어가는 선택 목록
            return result.Length > 0 ? result : new[] { new Vector2Int(maximumWidth, maximumHeight) };
        }

        public void setVolume(VolumeChannel channel, float value) //선택한 음량을 변경하고 저장 값 갱신
        {
            value = normalizeVolume(value);
            switch (channel)
            {
                case VolumeChannel.Master:
                    masterVolume = value;
                    PlayerPrefs.SetFloat(preferencePrefix + "Volume", value);
                    break;
                case VolumeChannel.Music:
                    musicVolume = value;
                    PlayerPrefs.SetFloat(preferencePrefix + "MusicVolume", value);
                    break;
                case VolumeChannel.Effects:
                    effectsVolume = value;
                    PlayerPrefs.SetFloat(preferencePrefix + "EffectsVolume", value);
                    break;
            }
        }

        public bool previewDisplay(Vector2Int resolution, bool useFullscreen) //해상도를 임시 적용하고 확인 시간 시작
        {
            if (displayPending || !resolutions.Contains(resolution))
                return false;
            if (Application.isEditor)
            {
                displayMessage = "해상도 변경은 Windows 실행 파일에서 적용됩니다.";
                return false;
            }
            previousResolution = new Vector2Int(Screen.width, Screen.height);
            previousMode = Screen.fullScreenMode;
            selectedResolution = resolution;
            fullscreen = useFullscreen;
            displayPending = true;
            previewFrame = Time.frameCount;
            displayDeadline = Time.realtimeSinceStartupAsDouble + 15;
            displayMessage = "화면이 잘 보이면 이 설정 유지를 눌러 주세요.";
            Screen.SetResolution(resolution.x, resolution.y, useFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            return true;
        }

        public void confirmDisplay() //사용자가 확인한 화면 설정만 영구 저장
        {
            if (!displayPending || Time.frameCount <= previewFrame)
                return;
            if (Time.realtimeSinceStartupAsDouble >= displayDeadline)
            {
                cancelDisplay();
                return;
            }
            displayPending = false;
            selectedResolution = new Vector2Int(Screen.width, Screen.height);
            fullscreen = Screen.fullScreen;
            PlayerPrefs.SetInt(preferencePrefix + "Width", selectedResolution.x);
            PlayerPrefs.SetInt(preferencePrefix + "Height", selectedResolution.y);
            PlayerPrefs.SetInt(preferencePrefix + "Fullscreen", fullscreen ? 1 : 0);
            displayMessage = "화면 설정을 저장했습니다.";
            save();
        }

        public void cancelDisplay() //취소하거나 확인 시간이 지난 해상도를 원래대로 복구
        {
            if (!displayPending)
                return;
            displayPending = false;
            selectedResolution = previousResolution;
            fullscreen = previousMode != FullScreenMode.Windowed;
            Screen.SetResolution(previousResolution.x, previousResolution.y, previousMode);
            displayMessage = "이전 화면 설정으로 돌아왔습니다.";
        }

        public void save() //변경한 설정을 디스크에 기록
        {
            PlayerPrefs.Save();
        }

        private void Update() //확인하지 않은 화면 변경의 자동 원복
        {
            if (displayPending && Time.realtimeSinceStartupAsDouble >= displayDeadline)
                cancelDisplay();
        }

        private void OnApplicationQuit() //종료 전 확정된 음량 저장
        {
            save();
        }
    }
}
