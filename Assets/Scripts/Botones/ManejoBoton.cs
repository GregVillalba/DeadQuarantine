using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ManejoBoton : MonoBehaviour
{
    [Serializable]
    public class OpcionConObjeto
    {
        public Button boton;
        public GameObject objetoAsociado; // Puede quedar vacío si el botón no necesita esto
    }

    [Header("Botones de opciones (con objeto opcional cada uno)")]
    [SerializeField] private List<OpcionConObjeto> opciones;

    [Header("Botón que se habilita al elegir 2")]
    [SerializeField] private Button botonAHabilitar;

    private List<Button> seleccionados = new List<Button>();

    private void Start()
    {
        if (botonAHabilitar != null)
            botonAHabilitar.gameObject.SetActive(false);

        foreach (OpcionConObjeto opcion in opciones)
        {
            if (opcion.objetoAsociado != null)
                opcion.objetoAsociado.SetActive(false);

            Button botonCapturado = opcion.boton;
            GameObject objetoCapturado = opcion.objetoAsociado;

            botonCapturado.onClick.AddListener(() => ToggleSeleccion(botonCapturado, objetoCapturado));
        }
    }

    private void ToggleSeleccion(Button boton, GameObject objetoAsociado)
    {
        Debug.Log("Toggle en: " + boton.name + " | Seleccionados antes: " + seleccionados.Count);

        bool estabaSeleccionado = seleccionados.Contains(boton);

        if (estabaSeleccionado)
        {
            seleccionados.Remove(boton);
            if (objetoAsociado != null)
                objetoAsociado.SetActive(false);
        }
        else
        {
            if (seleccionados.Count >= 2)
                return;

            seleccionados.Add(boton);
            if (objetoAsociado != null)
                objetoAsociado.SetActive(true);
        }
        Debug.Log("Seleccionados después: " + seleccionados.Count);

        VerificarHabilitacion();
    }

    private void VerificarHabilitacion()
    {
        if (botonAHabilitar != null)
            botonAHabilitar.gameObject.SetActive(seleccionados.Count == 2);
    }
}