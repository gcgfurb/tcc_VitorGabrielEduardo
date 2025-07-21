using UnityEngine;

public class TransmicaoManager : MonoBehaviour
{
    private MotorManager motor; 
    private float pedalEmbreagem, pedalEmbreagemInv, pedalAceleracao, rpmRodas;

    public void Acelerar(RodasManager rodas, MotorManager motor, float pedalEmbreagem, float pedalAceleracao, float kph, bool deveAcelerar)
    {
        this.pedalEmbreagem = pedalEmbreagem;
        this.pedalAceleracao = pedalAceleracao;
        this.rpmRodas = rodas.RodasRPM();
        this.pedalEmbreagemInv = Mathf.Abs(pedalEmbreagem - 1);
        this.motor = motor;

        float forcaAceleracaoMotor = CalcularForcaAceleracaoMotor(deveAcelerar);

        if (deveAcelerar)
        {
            float forcaFreioMotor = CalcularForcaFreioMotor(pedalEmbreagem, pedalAceleracao, motor, kph);
            float torqueAplicadoNaRoda = forcaAceleracaoMotor - forcaFreioMotor;
            rodas.Mover(torqueAplicadoNaRoda);
        }
        else
        {
            rodas.Mover(0f);
        }
    }

    private float CalcularForcaFreioMotor(float pedalEmbreagem, float pedalAceleracao, MotorManager motor, float kph)
    {
        // IF para o carro n�o andar pra tr�s
        if (kph < 1)
        {
            return 0f;
        }
        // S� age quando o pedal de acelera��o n�o for apertado
        // 0,05 tem uma margem para quando n�o detecta corretamente o pedal livre
        if (pedalAceleracao < 0.05f)
        {
            float forcaFreioMotor = motor.CalcularFreioMotor();
            return pedalEmbreagemInv * forcaFreioMotor;
        }
        return 0;
    }

    private float CalcularForcaAceleracaoMotor(bool deveAcelerar)
    {
        if (deveAcelerar)
            return FaseMotor();
        else
            return FaseMotorNeutro();
    }

    private float FaseMotorNeutro()
    {
        float rpmMotorAlvoMotor = motor.RpmAlvoMotorLivre(pedalAceleracao);
        rpmMotorAlvoMotor -= 30_000 * Time.deltaTime;
        rpmMotorAlvoMotor = rpmMotorAlvoMotor - (300 + motor.RpmMotor() * 15 * Time.deltaTime);
        motor.CalcularRpmMotor(rpmMotorAlvoMotor, 0.05f + 0.4f);
        return 0f;
    }

    private float FaseMotor()
    {
        // Pegar Valor inverso do pedal da embreagem
        // Enquanto mais toca no pedal, menos deve considerar a roda��o da roda
        float rpmMotorAlvoMotor = pedalEmbreagem * motor.RpmAlvoMotorLivre(pedalAceleracao);
        float rpmMotorAlvoRodas = pedalEmbreagemInv * rpmRodas * motor.RelacaoMarcha();
        float rpmMotorAlvo = rpmMotorAlvoMotor + rpmMotorAlvoRodas;

        rpmMotorAlvo -= 30_000 * Time.deltaTime;

        // Se a embreagem estiver muito apertada a velocidade com que o motor aumenta as rota��es aumenta tamb�m
        float impactoEmbreagem = (Mathf.Abs(pedalEmbreagem - 1) * 0.4f);
        float rpmMotorAtual = motor.CalcularRpmMotor(rpmMotorAlvo, 0.05f + impactoEmbreagem);

        // Se a rota��o alvo for menos que a livre deve matar o carro!!!
        motor.ValidarSeRpmMuitoBaixo(rpmMotorAtual, pedalEmbreagem);

        float torqueTotal = motor.CalcularPotenciaMotor(Mathf.Max(0.1f, pedalAceleracao)) * pedalEmbreagemInv;

        return torqueTotal;
    }



}
