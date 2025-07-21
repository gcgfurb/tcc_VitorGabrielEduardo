using Logitech;
using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CarroManager : MonoBehaviour
{

    private LogitechGSDK.DIJOYSTATE2ENGINES inputsLogi;
    private int _INDEX_LOGI = 0;
    private float _MAXIMO_INPUT_VOLANTE = 32768f;
    private int _DOWN_FORCE = 50;

    [SerializeField] private ControllerManager controllerManager;
    [SerializeField] private VolanteManager volante;
    [SerializeField] private RodasManager rodas;
    [SerializeField] private MotorManager motor;
    [SerializeField] private TransmicaoManager transmicao;
    [SerializeField] private bool offRoad = false;
    [SerializeField] private bool pistaMolhada = false;
    [SerializeField] private Transform pivotCameraVR;
    private Rigidbody carroRigidBody;
    private GameObject centroDeMassa;
    public bool motorLigado = false;
    private bool[] botoesPressionados = new bool[256];


    // Variveis do Carro
    private float embreagem = 0;
    private float freio = 0;
    private float acelerador = 0;
    private float rotacaoVolanteAbsoluta = 0;
    private float rotacaoVolanteAbsolutaNormalizado = 0;
    private float kph;
    private MarchaEnum marcha;
    private MarchaEnum marchaNova;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("SteeringInit:" + LogitechGSDK.LogiSteeringInitialize(false));
        carroRigidBody = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        AtualizarVariaveis();

        addDownForce();
        addEffects();

        volante.RotacionarVolante(rotacaoVolanteAbsoluta);
        rodas.GirarRodas(rotacaoVolanteAbsolutaNormalizado);

        if (controllerManager.ignicaoAcionada)
        {
            if (marchaNova != this.marcha)
            {
                marcha = marchaNova;
                motor.TrocarMarcha(marcha, embreagem, true);
            }
            transmicao.Acelerar(rodas, motor, embreagem, acelerador, kph, !motor.EhMarchaNeutra());
        }
        else if (!motor.EhMarchaNeutra())
        {
            motor.TrocarMarcha(MarchaEnum.NEUTRO, 1, false);
            transmicao.Acelerar(rodas, motor, 0f, 0f, kph, false);
        }

        rodas.FreiarMao(freio);
        rodas.FreiarPedal(freio);
    }

    void addEffects()
    {
        if (offRoad)
        {
            int forcaEfeito = (int) Mathf.Min(10 + kph / 1.5f, 75);
            LogitechGSDK.LogiPlayDirtRoadEffect(0, forcaEfeito);
        } 
        else
        {
            LogitechGSDK.LogiStopDirtRoadEffect(0);
        }
        if (pistaMolhada)
        {
            int forcaEfeito = (int)Mathf.Min(10 + kph / 1.2f, 65);
            LogitechGSDK.LogiPlaySlipperyRoadEffect(0, forcaEfeito);
        }
        else
        {
            LogitechGSDK.LogiStopSlipperyRoadEffect(0);
        }
    }

    void AtualizarVariaveis()
    {
        if (LogitechGSDK.LogiUpdate() && LogitechGSDK.LogiIsConnected(_INDEX_LOGI))
        {
            LogitechGSDK.LogiPlaySpringForce(_INDEX_LOGI, 0, 30, 60);
            inputsLogi = LogitechGSDK.LogiGetStateUnity(_INDEX_LOGI);

            embreagem = 0;
            freio = 0;
            acelerador = 0;
            if (!ValidarBugInicio(inputsLogi))
            {
                embreagem = NormalizarDadoPedal(inputsLogi.rglSlider[0]);
                freio = NormalizarDadoPedal(inputsLogi.lRz);
                acelerador = NormalizarDadoPedal(inputsLogi.lY);
            }

            rotacaoVolanteAbsoluta = inputsLogi.lX;
            rotacaoVolanteAbsolutaNormalizado = rotacaoVolanteAbsoluta / _MAXIMO_INPUT_VOLANTE;

            marchaNova = ObterMarchaAtual();
            CapturarValoresBotoesVolante();
        }

        kph = carroRigidBody.linearVelocity.magnitude * 3.6f;

        centroDeMassa = GameObject.Find("Massa");
        carroRigidBody.centerOfMass = centroDeMassa.transform.localPosition;
    }

    private void CapturarValoresBotoesVolante()
    {
        VerificarBotao(24, () => SceneManager.LoadScene("Menu")); // Bot�o PlayStation
        VerificarBotao(6, () => SceneManager.LoadScene(controllerManager.nomeCena)); // R2
        VerificarBotao(7, () => SceneManager.LoadScene(controllerManager.nomeCena)); // L2
        VerificarBotao(0, () => {
            offRoad = !offRoad;
            if (offRoad)
            {
                controllerManager.notificacao.MostrarNotificacao("Pista de Terra Ativada");
            }
            else
            {
                controllerManager.notificacao.MostrarNotificacao("Pista de Terra Desativada");
            }
        }); // Bot�o X
        VerificarBotao(1, () => {
            pistaMolhada = !pistaMolhada;
            if (pistaMolhada)
            {
                controllerManager.notificacao.MostrarNotificacao("Pista Molhada Ativada");
            }
            else
            {
                controllerManager.notificacao.MostrarNotificacao("Pista Molhada Desativada");
            }

        }); // Bot�o Quadrado
        VerificarBotao(2, () => {
            if (controllerManager.sintoAfivelado)
            {
                controllerManager.ignicaoAcionada = true;
                controllerManager.LigarCarro();
            }
            else
            {
                controllerManager.notificacao.MostrarNotificacao("Afivelar o cinto!");
            }
        }); // Bot�o Bolinha

        ObterValorDPad();
    }

    private void ObterValorDPad()
    {
        float valorAlteracao = 0.3f;
        Vector3 posicao = pivotCameraVR.position;
        switch (inputsLogi.rgdwPOV[0])
        {
            case (0): // UP
                posicao.z += valorAlteracao * Time.deltaTime; 
                break; 
            case (4500): // UP-RIGHT
                posicao.z += valorAlteracao * Time.deltaTime * 0.5f;
                posicao.x += valorAlteracao * Time.deltaTime * 0.5f;
                break; 
            case (9000): // RIGHT
                posicao.x += valorAlteracao * Time.deltaTime; 
                break; 
            case (13500): // DOWN-RIGHT
                posicao.x += valorAlteracao * Time.deltaTime * 0.5f;
                posicao.z -= valorAlteracao * Time.deltaTime * 0.5f;
                break; 
            case (18000): // DOWN
                posicao.z -= valorAlteracao * Time.deltaTime * 0.5f; 
                break; 
            case (22500): // DOWN-LEFT
                posicao.z -= valorAlteracao * Time.deltaTime * 0.5f;
                posicao.x -= valorAlteracao * Time.deltaTime * 0.5f;
                break;
            case (27000): // LEFT
                posicao.x -= valorAlteracao * Time.deltaTime; 
                break; 
            case (31500): // UP-LEFT
                posicao.x -= valorAlteracao * Time.deltaTime * 0.5f;
                posicao.z += valorAlteracao * Time.deltaTime * 0.5f;
                break; 
            default: // CENTER
                break; 
        }
        if (inputsLogi.rgbButtons[20] == 128)
        {
            posicao.y -= valorAlteracao * Time.deltaTime * 0.5f;
        } 
        else if (inputsLogi.rgbButtons[19] == 128)
        {
            posicao.y += valorAlteracao * Time.deltaTime * 0.5f;
        }
        Quaternion rotation = pivotCameraVR.rotation;

        if (inputsLogi.rgbButtons[21] == 128)
        {
            rotation.y -= valorAlteracao * Time.deltaTime * 0.3f;
        }
        else if (inputsLogi.rgbButtons[22] == 128)
        {
            rotation.y += valorAlteracao * Time.deltaTime * 0.3f;
        }
        pivotCameraVR.position = posicao;
        pivotCameraVR.rotation = rotation;
    }

    private void VerificarBotao(int indiceBotao, Action acao)
    {
        if (!botoesPressionados[indiceBotao] && inputsLogi.rgbButtons[indiceBotao] == 128)
        {
            botoesPressionados[indiceBotao] = true;
            acao.Invoke();
        }
        else if (inputsLogi.rgbButtons[indiceBotao] != 128)
        {
            botoesPressionados[indiceBotao] = false;
        }
    }

    public float VelocidadeTotal()
    {
        return kph;
    }

    public float RpmMotor()
    {
        return motor.RpmMotor(); ;
    }

    private MarchaEnum ObterMarchaAtual()
    {
        MarchaEnum marchaAtual = MarchaEnum.NEUTRO;
        if (inputsLogi.rgbButtons[12] == 128)
        {
            marchaAtual = MarchaEnum.PRIMEIRA;
        } 
        else if (inputsLogi.rgbButtons[13] == 128)
        {
            marchaAtual = MarchaEnum.SEGUNDA;
        }
        else if (inputsLogi.rgbButtons[14] == 128)
        {
            marchaAtual = MarchaEnum.TERCEIRA;
        }
        else if (inputsLogi.rgbButtons[15] == 128)
        {
            marchaAtual = MarchaEnum.QUARTA;
        }
        else if (inputsLogi.rgbButtons[16] == 128)
        {
            marchaAtual = MarchaEnum.QUINTA;
        }
        else if (inputsLogi.rgbButtons[17] == 128)
        {
            marchaAtual = MarchaEnum.SEXTA;
        }
        else if (inputsLogi.rgbButtons[18] == 128)
        {
            marchaAtual = MarchaEnum.RE;
        }

        return marchaAtual;
    }

    void OnApplicationQuit()
    {
        Debug.Log("SteeringShutdown:" + LogitechGSDK.LogiSteeringShutdown());
    }

    float NormalizarDadoPedal(float dadoBruto)
    {
        return 1 - (dadoBruto + 32768) / 65535;
    }

    // Quando o jogo inicia antes de apertar em qualquer pedal o input deles � marcado como todos em 0 ao mesmo tempo
    bool ValidarBugInicio(LogitechGSDK.DIJOYSTATE2ENGINES rec)
    {
        return rec.rglSlider[0] == 0f && rec.lRz == 0f && rec.lY == 0f;
    }

    void addDownForce()
    {
        carroRigidBody.AddForce(-transform.up * _DOWN_FORCE * carroRigidBody.linearVelocity.magnitude);
    }

}
