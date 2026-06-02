using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.Collections;
using UnityEngine.XR.ARSubsystems;
using ZXing;
using Unity.XR.CoreUtils; 

public class QrCodeRecenter : MonoBehaviour
{
    [SerializeField] private ARSession session;
    [SerializeField] private XROrigin sessionOrigin; 
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private List<Target> navigationTargetObjects = new List<Target>();

    [Header("Optimization")]
    [SerializeField] [Range(0.1f, 1f)] private float scanInterval = 0.5f; // Quét 2 lần/giây
    private float scanTimer = 0f;

    private Texture2D cameraImageTexture;
    private IBarcodeReader reader;

    private void Start()
    {
        reader = new BarcodeReader
        {
            AutoRotate = true, 
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true, 
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE } // Chỉ tập trung quét QR
            }
        };
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SetQrCodeRecenterTarget("Bedroom1");
        }
        
        scanTimer += Time.deltaTime;
    }

    private void OnEnable()
    {
        cameraManager.frameReceived += OnCameraFrameReceived;
    }

    private void OnDisable()
    {
        cameraManager.frameReceived -= OnCameraFrameReceived;
    }

    private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // Process frame only at defined intervals to optimize performance
        if (scanTimer < scanInterval) return;

        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) return;

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            // Reduce resolution for faster processing
            outputDimensions = new Vector2Int(image.width / 3, image.height / 3), 
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        int size = image.GetConvertedDataSize(conversionParams);
        var buffer = new NativeArray<byte>(size, Allocator.Temp);
        image.Convert(conversionParams, buffer);
        image.Dispose(); 

        if (cameraImageTexture == null || cameraImageTexture.width != conversionParams.outputDimensions.x)
        {
            if (cameraImageTexture != null) Destroy(cameraImageTexture);
            cameraImageTexture = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, conversionParams.outputFormat, false);
        }

        cameraImageTexture.LoadRawTextureData(buffer);
        cameraImageTexture.Apply();
        buffer.Dispose();

        // Decode QR code from the camera image
        var result = reader.Decode(cameraImageTexture.GetPixels32(), cameraImageTexture.width, cameraImageTexture.height);
        
        if (result != null)
        {
            Debug.Log($"[QR CODE DETECTED]: {result.Text}");
            SetQrCodeRecenterTarget(result.Text);
            
            scanTimer = -1.0f; 
        }
        else
        {
            scanTimer = 0f;
        }
    }

    private void SetQrCodeRecenterTarget(string targetText)
    {
        Target currentTarget = navigationTargetObjects.Find(x => x.Name.ToLower().Equals(targetText.ToLower()));
        if (currentTarget != null)
        {
            //session.Reset();
            sessionOrigin.transform.position = currentTarget.PositionObject.transform.position;
            sessionOrigin.transform.rotation = currentTarget.PositionObject.transform.rotation;
            Debug.Log($"[RECENTER SUCCESS] Mapped to: {currentTarget.Name}");
        }
        else
        {
            Debug.LogWarning($"[RECENTER FAILED] Cannot find target with name: {targetText}");
        }
    }

    public void ChangeActiveFloor(string floorEntrance)
    {
        SetQrCodeRecenterTarget(floorEntrance);
    }
}