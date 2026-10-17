# Recorridos de prueba locales

Usar solo datos sinteticos. No ejecutar contra el entorno original. Las pruebas cambian la base local, que no se sube a Git. Para una sesion de pruebas aislada se puede configurar `Storage__DatabasePath` con otra ruta dentro de `App_Data` antes de arrancar la API; no borrar ni sustituir una base existente.

Arrancar API en Development y Angular segun el [README](../README.md). Abrir `http://localhost:4200/login`. Los perfiles disponibles dependen de las empresas aprobadas existentes en la base.

## 1. Cargador: crear una nueva oportunidad

1. Entrar como `Charterer One` y crear una carga con referencia propia identificable.
2. Completar cantidad, mercancia, puertos y laycan; invitar a Northstar Shipping y Baltic Meridian.
3. Revisar el ultimo paso y confirmar. Debe aparecer una referencia del servidor y la tarjeta en Cargo Orders.
4. Volver al dashboard. No debe anunciarse un correo enviado.

## 2. Armadores: responder y actualizar

1. Cambiar al perfil Northstar Shipping. La carga invitada debe mostrar `Submit offer`.
2. Abrir el formulario. Debe mostrar la solicitud original y no propuestas de competidores.
3. Escribir un flete positivo, su moneda/unidad y condiciones; guardar.
4. Volver a abrir y actualizar el flete. Debe existir una sola propuesta propia, con el mismo identificador, no dos columnas.
5. Cambiar a Baltic Meridian y presentar otra propuesta para esa carga.
6. Volver a Northstar: debe seguir viendo solo su propia propuesta. Si se ha quedado abierto un formulario con version antigua, el servidor puede devolver 409; debe conservar los cambios escritos y no anunciar un guardado correcto.

## 3. Cargador: comparar y seleccionar

1. Cambiar a Charterer One y abrir la comparativa de la carga nueva.
2. Deben verse las dos propuestas con condiciones, moneda, unidades y fecha de recepcion.
3. Elegir `Offer firm` para una. Solo tras aceptarlo la API debe abrirse la negociacion con esas condiciones.
4. El armador no seleccionado no debe poder acceder al expediente de negociacion. El seleccionado si.
5. Ningun armador debe poder seguir modificando la oferta inicial una vez seleccionada una propuesta.

## 4. Negociacion y Recap por partes

1. Tras seleccionar la oferta, el turno debe ser del armador. Como cargador, comprobar el aviso de lectura y que condiciones, clausulas y envio estan deshabilitados. Cambiar al armador elegido, modificar condiciones y sus clausulas y enviar.
2. Comprobar que el contador sube una unidad y el turno pasa al cargador; el armador no debe poder enviar otra vez. Entrar como cargador y refrescar. Enviar una respuesta modificando solo sus propias clausulas. Desplegar `COUNTER OFFER HISTORY`: deben figurar ambos envios, sus autores/fechas UTC y los valores antes/enviados. Guardar las condiciones antes de preparar el Recap y comprobar que coinciden.
3. Como cargador, debe aparecer solo su boton de confirmacion. Confirmarlo no cierra todavia la operacion.
4. Cambiar al armador seleccionado y confirmar su parte. La operacion debe pasar a Operative.
5. Cambiar al administrador: puede consultar, pero no firmar por una u otra parte.
6. La aceptacion individual por campo todavia no esta implementada. El historial de operaciones antiguas puede estar vacio; no se reconstruye actividad anterior.

### Borrador privado

1. En el turno propio, escribir un flete incompleto (por ejemplo `123-`) y modificar una clausula propia. Pulsar `SAVE DRAFT`: debe anunciar guardado privado, no envio. Contador, turno y condiciones enviadas no deben cambiar.
2. Salir y volver a la negociacion. Debe mostrar las condiciones enviadas y el aviso del borrador, sin restaurarlo automaticamente. Pulsar `RESTORE DRAFT (REPLACE EDITS)` y comprobar campos/clausulas.
3. Cambiar al otro participante o al administrador: no debe ver el borrador privado.
4. Con el mismo usuario, eliminar el borrador: desaparece el guardado, pero los campos actuales no cambian. Guardar otro, completar valores validos y enviar contraoferta: desaparece el borrador del remitente.
5. Si otro usuario del mismo lado cambia la negociacion, al volver el borrador anterior debe indicar que esta obsoleto y no permitir restaurarlo. Puede eliminarse sin tocar la negociacion. No hay autoguardado ni fusion de versiones.

## 5. Persistencia y sesiones

1. Apagar y reiniciar la API con la misma ruta de base de datos.
2. Volver a elegir perfil: las sesiones anteriores deben caducar, pero la carga, propuestas, condiciones, turnos, historial, borradores privados y confirmaciones deben conservarse.
3. Crear otra carga: la nueva referencia no debe colisionar con las existentes ni duplicarse la semilla demo.

## 6. Casos negativos

- Flete vacio, cero/negativo, moneda invalida o cantidad no positiva: no guardar ni mostrar exito.
- Cargador o administrador accediendo directamente a `/offers/{id}/bid`: no puede enviar como armador.
- Armador no invitado: no puede leer/responder a esa carga, aunque conozca su identificador.
- Modificar el identificador de armador en una peticion: la API debe impedir actuar como otra empresa.
- Version antigua tras otra respuesta o seleccion: no sobreescribir datos nuevos.
- API no disponible: mostrar error, sin sustituir los datos por cotizaciones ficticias.

Los fallos SQLite y las reglas de acceso tambien tienen pruebas automatizadas. No simular una averia modificando una base con datos reales.

## 7. Alcance de la verificacion

Ejecutar `dotnet test OceanFixture.sln --configuration Release` y `npm run test:ci`; compilar .NET y Angular. Las pruebas unitarias y comprobaciones HTTP no sustituyen validar todas las pantallas, accesibilidad, varios usuarios reales ni el despliegue de produccion.
