FROM ghcr.io/btbn/ffmpeg-builds/base-win64:latest AS base
ENV TARGET=win64 VARIANT=gpl-shared REPO=btbn/ffmpeg-builds ADDINS_STR=7.1
COPY --link util/run_stage.sh /usr/bin/run_stage
FROM base AS layer-10-mingw-std-threads
ENV SELF="scripts.d/10-mingw-std-threads.sh" STAGENAME="10-mingw-std-threads"
RUN --mount=src=scripts.d/10-mingw-std-threads.sh,dst=/stage.sh --mount=src=.cache/downloads/10-mingw-std-threads_f6fe5dc5a067e90cd8b37a748b33962ac2eb09f3b486b287265803070dbf02d2.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM base AS layer-10-mingw
ENV SELF="scripts.d/10-mingw.sh" STAGENAME="10-mingw"
RUN --mount=src=scripts.d/10-mingw.sh,dst=/stage.sh --mount=src=.cache/downloads/10-mingw_e92064005ae43ad00ffef599ef07d3140ca1d8bf2649d15cf4d5ae39053cee92.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM base AS layer-10-xorg-macros
FROM base AS layer-10
COPY --link --from=layer-10-mingw-std-threads $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-10-mingw /opt/mingw/. /
COPY --link --from=layer-10-mingw /opt/mingw/. /opt/mingw
FROM layer-10 AS layer-20-libiconv
ENV SELF="scripts.d/20-libiconv.sh" STAGENAME="20-libiconv"
RUN --mount=src=scripts.d/20-libiconv.sh,dst=/stage.sh --mount=src=.cache/downloads/20-libiconv_05554a424c1e9ef734c0704eabd43b322e9eed0ec55a02585c4e7c7924465344.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-10 AS layer-20-zlib
ENV SELF="scripts.d/20-zlib.sh" STAGENAME="20-zlib"
RUN --mount=src=scripts.d/20-zlib.sh,dst=/stage.sh --mount=src=.cache/downloads/20-zlib_de6eb5b0c023ea9ddc26a69d252abf44795385e9abde9d51d303eca1a8f733a8.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-10 AS layer-20
COPY --link --from=layer-20-libiconv $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-20-zlib $FFBUILD_PREFIX/. $FFBUILD_PREFIX
FROM layer-20 AS layer-25-fftw3
ENV SELF="scripts.d/25-fftw3.sh" STAGENAME="25-fftw3"
RUN --mount=src=scripts.d/25-fftw3.sh,dst=/stage.sh --mount=src=.cache/downloads/25-fftw3_f4402e66e50a92f9be4a56ba1a5bd34e4cf26dcbb2d7c84832b32f1a5c46258d.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-20 AS layer-25-freetype
ENV SELF="scripts.d/25-freetype.sh" STAGENAME="25-freetype"
RUN --mount=src=scripts.d/25-freetype.sh,dst=/stage.sh --mount=src=.cache/downloads/25-freetype_42fafa02409fc73e698e3ab87b9c5ba165e6d8e7c8a6430e19ac56f29308d09c.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-20 AS layer-25-fribidi
ENV SELF="scripts.d/25-fribidi.sh" STAGENAME="25-fribidi"
RUN --mount=src=scripts.d/25-fribidi.sh,dst=/stage.sh --mount=src=.cache/downloads/25-fribidi_2b64cf3a2818a8a4fa674201ec07ca60ffcb6f07e0684c5de0a4924aec9d63b2.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-20 AS layer-25-gmp
ENV SELF="scripts.d/25-gmp.sh" STAGENAME="25-gmp"
RUN --mount=src=scripts.d/25-gmp.sh,dst=/stage.sh --mount=src=.cache/downloads/25-gmp_94c60ee0768eca71202d17fc4fb4047ea322c2baf362b51895aeb000544989b4.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-20 AS layer-25-libogg
ENV SELF="scripts.d/25-libogg.sh" STAGENAME="25-libogg"
RUN --mount=src=scripts.d/25-libogg.sh,dst=/stage.sh --mount=src=.cache/downloads/25-libogg_14e8aefbd7a7d38c6742d56b00825459a426cfde3a92eae66a5a312150c8372b.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-20 AS layer-25-libxml2
ENV SELF="scripts.d/25-libxml2.sh" STAGENAME="25-libxml2"
RUN --mount=src=scripts.d/25-libxml2.sh,dst=/stage.sh --mount=src=.cache/downloads/25-libxml2_b56ab88eb82aedc62d1fc6db887a668cafa9e85ffe8a0ca813d7a0294e617b24.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-20 AS layer-25-openssl
ENV SELF="scripts.d/25-openssl.sh" STAGENAME="25-openssl"
RUN --mount=src=scripts.d/25-openssl.sh,dst=/stage.sh --mount=src=.cache/downloads/25-openssl_34a3da05a5e03800e7bf55c3df455372d31d7f7c7e770298ab7d7022b2590249.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-20 AS layer-25-xz
ENV SELF="scripts.d/25-xz.sh" STAGENAME="25-xz"
RUN --mount=src=scripts.d/25-xz.sh,dst=/stage.sh --mount=src=.cache/downloads/25-xz_b9e660d6635ef8ae6309df5eeefc2e716561b1b1560e6e2aff5df36d46e80566.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-20 AS layer-25
COPY --link --from=layer-25-fftw3 $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-25-freetype $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-25-fribidi $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-25-gmp $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-25-libogg $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-25-libxml2 $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-25-openssl $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-25-xz $FFBUILD_PREFIX/. $FFBUILD_PREFIX
FROM layer-25 AS layer-35-fontconfig
ENV SELF="scripts.d/35-fontconfig.sh" STAGENAME="35-fontconfig"
RUN --mount=src=scripts.d/35-fontconfig.sh,dst=/stage.sh --mount=src=.cache/downloads/35-fontconfig_01987173b9aa3d78ccd3f09f47bfa33a5d132703aadb398ad7d63c6f9237d37f.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-25 AS layer-35
COPY --link --from=layer-35-fontconfig $FFBUILD_PREFIX/. $FFBUILD_PREFIX
FROM layer-35 AS layer-45-harfbuzz
ENV SELF="scripts.d/45-harfbuzz.sh" STAGENAME="45-harfbuzz"
RUN --mount=src=scripts.d/45-harfbuzz.sh,dst=/stage.sh --mount=src=.cache/downloads/45-harfbuzz_b126262d88fb4e650bade0b626e009b2e84e068e1db76f8e6d4327b4757e1caa.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-35 AS layer-45-libsamplerate
ENV SELF="scripts.d/45-libsamplerate.sh" STAGENAME="45-libsamplerate"
RUN --mount=src=scripts.d/45-libsamplerate.sh,dst=/stage.sh --mount=src=.cache/downloads/45-libsamplerate_ef6bcafdf37112c2d41dcb6f9bb022daa836c48200f27ab9cf633a639886cf58.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-35 AS layer-45-libudfread
ENV SELF="scripts.d/45-libudfread.sh" STAGENAME="45-libudfread"
RUN --mount=src=scripts.d/45-libudfread.sh,dst=/stage.sh --mount=src=.cache/downloads/45-libudfread_055df33ac134c283339e0ad578500a8d02c8620b7158ec425bd317a6b5f660df.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-35 AS layer-45-libvorbis
ENV SELF="scripts.d/45-libvorbis.sh" STAGENAME="45-libvorbis"
RUN --mount=src=scripts.d/45-libvorbis.sh,dst=/stage.sh --mount=src=.cache/downloads/45-libvorbis_43a45f8ab756a8f9ff53dc1e73dd2be011e4116675529540ef42bc19c4d81c00.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-35 AS layer-45-opencl
ENV SELF="scripts.d/45-opencl.sh" STAGENAME="45-opencl"
RUN --mount=src=scripts.d/45-opencl.sh,dst=/stage.sh --mount=src=.cache/downloads/45-opencl_96a1545c8a0d20b95f6d4dc2fc16132894e8c6e585c4988b931aed2a940c003c.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-35 AS layer-45-pulseaudio
FROM layer-35 AS layer-45-vmaf
ENV SELF="scripts.d/45-vmaf.sh" STAGENAME="45-vmaf"
RUN --mount=src=scripts.d/45-vmaf.sh,dst=/stage.sh --mount=src=.cache/downloads/45-vmaf_4955f1e442d7b263295ae549d3c00e403d285bf8b2d422a1a8476478be9c7fa5.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-35 AS layer-45-x11
FROM layer-35 AS layer-45
COPY --link --from=layer-45-harfbuzz $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-45-libsamplerate $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-45-libudfread $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-45-libvorbis $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-45-opencl $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-45-vmaf $FFBUILD_PREFIX/. $FFBUILD_PREFIX
FROM layer-45 AS layer-50-amf
ENV SELF="scripts.d/50-amf.sh" STAGENAME="50-amf"
RUN --mount=src=scripts.d/50-amf.sh,dst=/stage.sh --mount=src=.cache/downloads/50-amf_6d8ed4c7a4cfdc2a83d9c247ec4420a0dd6765c9a4380d316c7f9e6b560611a1.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-aom
ENV SELF="scripts.d/50-aom.sh" STAGENAME="50-aom"
RUN --mount=src=scripts.d/50-aom.sh,dst=/stage.sh --mount=src=.cache/downloads/50-aom_ad881c9a106598aafc53054ee13640b255dce05ac5872d999177f896fdaa585c.tar.xz,dst=/cache.tar.xz --mount=src=patches/aom,dst=/patches run_stage /stage.sh
FROM layer-45 AS layer-50-aribb24
ENV SELF="scripts.d/50-aribb24/25-libpng.sh" STAGENAME="25-libpng"
RUN --mount=src=scripts.d/50-aribb24/25-libpng.sh,dst=/stage.sh --mount=src=.cache/downloads/25-libpng_94be2649485726af4abc82325f02cc1070634f2162ee346dfe3afc0c9ad4138e.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-aribb24/50-libaribb24.sh" STAGENAME="50-libaribb24"
RUN --mount=src=scripts.d/50-aribb24/50-libaribb24.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libaribb24_3dd74ffb861f7ef9133dab074b9845408d84ebc49db9aee5d91829efba6d52ce.tar.xz,dst=/cache.tar.xz --mount=src=patches/aribb24,dst=/patches run_stage /stage.sh
FROM layer-45 AS layer-50-avisynth
ENV SELF="scripts.d/50-avisynth.sh" STAGENAME="50-avisynth"
RUN --mount=src=scripts.d/50-avisynth.sh,dst=/stage.sh --mount=src=.cache/downloads/50-avisynth_303994cf9965cce8405f02070a0348d32a82583ec67a208068f16ab12df0a094.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-chromaprint
ENV SELF="scripts.d/50-chromaprint.sh" STAGENAME="50-chromaprint"
RUN --mount=src=scripts.d/50-chromaprint.sh,dst=/stage.sh --mount=src=.cache/downloads/50-chromaprint_049029052411ba569876b7529f6a4391e2e5cee85eaed0d24922f5e44e2e25a9.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-dav1d
ENV SELF="scripts.d/50-dav1d.sh" STAGENAME="50-dav1d"
RUN --mount=src=scripts.d/50-dav1d.sh,dst=/stage.sh --mount=src=.cache/downloads/50-dav1d_45bf4e7f0504e45b915c866bf26a448c0d1d9a9235e0010fd462861a1eb95337.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-davs2
ENV SELF="scripts.d/50-davs2.sh" STAGENAME="50-davs2"
RUN --mount=src=scripts.d/50-davs2.sh,dst=/stage.sh --mount=src=.cache/downloads/50-davs2_a2d3a5d05dbd810e7a33f41ca77f70f1775ab8318ffe3e1b365105e8ad813d7e.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-dvd
ENV SELF="scripts.d/50-dvd/30-libdvdcss.sh" STAGENAME="30-libdvdcss"
RUN --mount=src=scripts.d/50-dvd/30-libdvdcss.sh,dst=/stage.sh --mount=src=.cache/downloads/30-libdvdcss_efac986ff210298b2dd97b431c516fcdf191c508d4d9553543d7853b20324b5a.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-dvd/40-libdvdread.sh" STAGENAME="40-libdvdread"
RUN --mount=src=scripts.d/50-dvd/40-libdvdread.sh,dst=/stage.sh --mount=src=.cache/downloads/40-libdvdread_77ea98d38ad05c70fdfd25cf71c856aa4cdfb9a61483452172dbcad5703e7b6f.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-dvd/50-libdvdnav.sh" STAGENAME="50-libdvdnav"
RUN --mount=src=scripts.d/50-dvd/50-libdvdnav.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libdvdnav_b67679e11541c72602addb140ce1955274328f6a5b489b866c7e26fa150d2e3d.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-fdk-aac
FROM layer-45 AS layer-50-ffnvcodec
ENV SELF="scripts.d/50-ffnvcodec.sh" STAGENAME="50-ffnvcodec"
RUN --mount=src=scripts.d/50-ffnvcodec.sh,dst=/stage.sh --mount=src=.cache/downloads/50-ffnvcodec_3fc0ad87726b1291a17e674ac32e366cf2423a42018c0ed88efcf71f32e4ab45.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-frei0r
ENV SELF="scripts.d/50-frei0r.sh" STAGENAME="50-frei0r"
RUN --mount=src=scripts.d/50-frei0r.sh,dst=/stage.sh --mount=src=.cache/downloads/50-frei0r_085e7e570bb63a926a5ada80a4fcf5ee7d8d9a20ddc48e9f222a8a6fce94c1e3.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-gme
ENV SELF="scripts.d/50-gme.sh" STAGENAME="50-gme"
RUN --mount=src=scripts.d/50-gme.sh,dst=/stage.sh --mount=src=.cache/downloads/50-gme_5a200773288803418ed158790820c319365f776207d5430bfb202929e969b9a7.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-kvazaar
ENV SELF="scripts.d/50-kvazaar.sh" STAGENAME="50-kvazaar"
RUN --mount=src=scripts.d/50-kvazaar.sh,dst=/stage.sh --mount=src=.cache/downloads/50-kvazaar_2435154058c128e13c22ceb7ecd59c10a64825b96deeec1334cbbd965eba267f.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libaribcaption
ENV SELF="scripts.d/50-libaribcaption.sh" STAGENAME="50-libaribcaption"
RUN --mount=src=scripts.d/50-libaribcaption.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libaribcaption_b6eb52cb76080bf1b08d439deb03636b55ce56450c97a0812f6134981334aaff.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libass
ENV SELF="scripts.d/50-libass.sh" STAGENAME="50-libass"
RUN --mount=src=scripts.d/50-libass.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libass_cd3b56a661e354c2f86f643cdd5109fd77e731a2ce2b8781396cb38d74c96c3e.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libbluray
ENV SELF="scripts.d/50-libbluray.sh" STAGENAME="50-libbluray"
RUN --mount=src=scripts.d/50-libbluray.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libbluray_fafb21cf58c8b837f76ca42c83324342d0e9d087eb15f63b7c9021cdea9685e6.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libjxl
ENV SELF="scripts.d/50-libjxl/45-brotli.sh" STAGENAME="45-brotli"
RUN --mount=src=scripts.d/50-libjxl/45-brotli.sh,dst=/stage.sh --mount=src=.cache/downloads/45-brotli_fcef3d1ccf70df931c662c782747ae1033141d33a72f6380413e446b4d8bcc24.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-libjxl/45-lcms2.sh" STAGENAME="45-lcms2"
RUN --mount=src=scripts.d/50-libjxl/45-lcms2.sh,dst=/stage.sh --mount=src=.cache/downloads/45-lcms2_e79b7427a246de9b45daa13f2b92ac58178fd3c366325261a137bf49aafdeaaa.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-libjxl/50-libjxl.sh" STAGENAME="50-libjxl"
RUN --mount=src=scripts.d/50-libjxl/50-libjxl.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libjxl_80f040347f936ed722f20cbd440298dc438bd2902f966dda14c5e3c3b8f1f338.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libmp3lame
ENV SELF="scripts.d/50-libmp3lame.sh" STAGENAME="50-libmp3lame"
RUN --mount=src=scripts.d/50-libmp3lame.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libmp3lame_572043a74090d765071c155bc1b107920cde3add6ff61932e789f12d75e0c887.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libopus
ENV SELF="scripts.d/50-libopus.sh" STAGENAME="50-libopus"
RUN --mount=src=scripts.d/50-libopus.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libopus_d81cc0e7f7d6941d9560e8008de356e5acf49a6ab42c3e6d8da07133a0cc022f.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-librist
ENV SELF="scripts.d/50-librist/40-mbedtls.sh" STAGENAME="40-mbedtls"
RUN --mount=src=scripts.d/50-librist/40-mbedtls.sh,dst=/stage.sh --mount=src=.cache/downloads/40-mbedtls_592ca3c9f5f9abc78e611b6eff63ffc6db9161baaa439941a2bf7a07669f84ae.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-librist/50-librist.sh" STAGENAME="50-librist"
RUN --mount=src=scripts.d/50-librist/50-librist.sh,dst=/stage.sh --mount=src=.cache/downloads/50-librist_52441b2f51e8babb274978e8be7c34d1c4b2ed0fc36fa2b6b5f36247c71b56dd.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libssh
ENV SELF="scripts.d/50-libssh.sh" STAGENAME="50-libssh"
RUN --mount=src=scripts.d/50-libssh.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libssh_9b547c5922ea52dd9af01179097ef96c5f9e53432e7e0b53b273183b1d28a1a4.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libtheora
ENV SELF="scripts.d/50-libtheora.sh" STAGENAME="50-libtheora"
RUN --mount=src=scripts.d/50-libtheora.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libtheora_a89006da0d76cdba1d23f1f3dcb15102436c14593b7488b53c9126eabba15e73.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libvpx
ENV SELF="scripts.d/50-libvpx.sh" STAGENAME="50-libvpx"
RUN --mount=src=scripts.d/50-libvpx.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libvpx_e77e0f0d6dd1ea59b8e22ada3659827fe9e53a9726798c7705dd138cb164fdcc.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libwebp
ENV SELF="scripts.d/50-libwebp.sh" STAGENAME="50-libwebp"
RUN --mount=src=scripts.d/50-libwebp.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libwebp_a11b4ba2122fb7203e1e04480bbd63c89331bb3cfd6bc5ec82a8a3409b5fa7f7.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-libzmq
ENV SELF="scripts.d/50-libzmq.sh" STAGENAME="50-libzmq"
RUN --mount=src=scripts.d/50-libzmq.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libzmq_1a7bc869bc42c034185bf4c6dc3fea1aed909f6109a47f6b1a5ee51a84e8d75f.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-lilv
ENV SELF="scripts.d/50-lilv/96-lv2.sh" STAGENAME="96-lv2"
RUN --mount=src=scripts.d/50-lilv/96-lv2.sh,dst=/stage.sh --mount=src=.cache/downloads/96-lv2_9c3bd26ab6f99040a36a945493bfb45b95ec245689b48b019563c7d44a166922.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-lilv/96-serd.sh" STAGENAME="96-serd"
RUN --mount=src=scripts.d/50-lilv/96-serd.sh,dst=/stage.sh --mount=src=.cache/downloads/96-serd_399d93a7cf68c32f96846d1219c7e3f5fe08f54691a52a3fc3d39f41f50e6bf5.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-lilv/96-zix.sh" STAGENAME="96-zix"
RUN --mount=src=scripts.d/50-lilv/96-zix.sh,dst=/stage.sh --mount=src=.cache/downloads/96-zix_49e6608f897e9fc9b085f5d4b96f541621e49e04a376a6f25505d03191def5c2.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-lilv/97-sord.sh" STAGENAME="97-sord"
RUN --mount=src=scripts.d/50-lilv/97-sord.sh,dst=/stage.sh --mount=src=.cache/downloads/97-sord_de563a15847e0fd770701cf28e8cc2908923721bdc1bb5020174c93830d09440.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-lilv/98-sratom.sh" STAGENAME="98-sratom"
RUN --mount=src=scripts.d/50-lilv/98-sratom.sh,dst=/stage.sh --mount=src=.cache/downloads/98-sratom_6ccd19d548e535a534df3e9d613d5e77c85b957edce59cac2aa8ec72a2e7146c.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-lilv/99-lilv.sh" STAGENAME="99-lilv"
RUN --mount=src=scripts.d/50-lilv/99-lilv.sh,dst=/stage.sh --mount=src=.cache/downloads/99-lilv_a959a1e108d6b66280e90465df9ce9d235ad6b63ea97bde8a7123425c36f004b.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-onevpl
ENV SELF="scripts.d/50-onevpl.sh" STAGENAME="50-onevpl"
RUN --mount=src=scripts.d/50-onevpl.sh,dst=/stage.sh --mount=src=.cache/downloads/50-onevpl_0f1620ea8a2a815743481dc8f6b563e22ceb5edd7248acebc62b0d86da8ae490.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-openal
ENV SELF="scripts.d/50-openal.sh" STAGENAME="50-openal"
RUN --mount=src=scripts.d/50-openal.sh,dst=/stage.sh --mount=src=.cache/downloads/50-openal_1006b7db02910a44e5d415153aec80efbdaf7f5929d4fd958c8c3ac90f6a89bc.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-opencore-amr
ENV SELF="scripts.d/50-opencore-amr.sh" STAGENAME="50-opencore-amr"
RUN --mount=src=scripts.d/50-opencore-amr.sh,dst=/stage.sh --mount=src=.cache/downloads/50-opencore-amr_5d7ec3b8ffdd0f32309a659256916e3286724f08acf0f625a4b4f1f9c17ebf7c.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-openh264
ENV SELF="scripts.d/50-openh264.sh" STAGENAME="50-openh264"
RUN --mount=src=scripts.d/50-openh264.sh,dst=/stage.sh --mount=src=.cache/downloads/50-openh264_d85bbd52b8f497fd2901b636ff573e523080a5f32983f4e96eee8a858a2af122.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-openjpeg
ENV SELF="scripts.d/50-openjpeg.sh" STAGENAME="50-openjpeg"
RUN --mount=src=scripts.d/50-openjpeg.sh,dst=/stage.sh --mount=src=.cache/downloads/50-openjpeg_7f7caaa3c7bcacce8cf49269536e88ae4947ec0f660fdc72cf65109ce76bff40.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-openmpt
ENV SELF="scripts.d/50-openmpt.sh" STAGENAME="50-openmpt"
RUN --mount=src=scripts.d/50-openmpt.sh,dst=/stage.sh --mount=src=.cache/downloads/50-openmpt_f2f0d333ed59f7a44b5a0572a69e41c6f82b593db54c9bffa49f061843b201db.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-rav1e
ENV SELF="scripts.d/50-rav1e.sh" STAGENAME="50-rav1e"
RUN --mount=src=scripts.d/50-rav1e.sh,dst=/stage.sh --mount=src=.cache/downloads/50-rav1e_02864f87c4421f83cbe7c85639ee51e66ad878784bdf215e96c54cd116bb231b.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-rubberband
ENV SELF="scripts.d/50-rubberband.sh" STAGENAME="50-rubberband"
RUN --mount=src=scripts.d/50-rubberband.sh,dst=/stage.sh --mount=src=.cache/downloads/50-rubberband_ebcdeaff89e37848c70b3fbd5e5505e769f6abe0e8fc53422d166644840d30c2.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-schannel
ENV SELF="scripts.d/50-schannel.sh" STAGENAME="50-schannel"
FROM layer-45 AS layer-50-sdl
ENV SELF="scripts.d/50-sdl.sh" STAGENAME="50-sdl"
RUN --mount=src=scripts.d/50-sdl.sh,dst=/stage.sh --mount=src=.cache/downloads/50-sdl_fb3ff3fc537e425bd1b5dd9bdf868bccd36cf115a4c02f9eaa06ad73c017faf5.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-soxr
ENV SELF="scripts.d/50-soxr.sh" STAGENAME="50-soxr"
RUN --mount=src=scripts.d/50-soxr.sh,dst=/stage.sh --mount=src=.cache/downloads/50-soxr_d24c000584012b62b406f09325af85575932219b972b87ab3e2b1037bdc97c6a.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-srt
ENV SELF="scripts.d/50-srt.sh" STAGENAME="50-srt"
RUN --mount=src=scripts.d/50-srt.sh,dst=/stage.sh --mount=src=.cache/downloads/50-srt_7285cb0b03ddd6d1e01b5d11b76290189abccf1db7cffc179f06c5a87f8e0e5a.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-svtav1
ENV SELF="scripts.d/50-svtav1.sh" STAGENAME="50-svtav1"
RUN --mount=src=scripts.d/50-svtav1.sh,dst=/stage.sh --mount=src=.cache/downloads/50-svtav1_1571c625785747072088d783947bfbd18dcc7016f6446192f26bdbe0a6cc7d1a.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-twolame
ENV SELF="scripts.d/50-twolame.sh" STAGENAME="50-twolame"
RUN --mount=src=scripts.d/50-twolame.sh,dst=/stage.sh --mount=src=.cache/downloads/50-twolame_0222bab9e1e6869937be0713572586ff12480843520d525ff38cd76b4667a7a2.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-uavs3d
ENV SELF="scripts.d/50-uavs3d.sh" STAGENAME="50-uavs3d"
RUN --mount=src=scripts.d/50-uavs3d.sh,dst=/stage.sh --mount=src=.cache/downloads/50-uavs3d_232be137fb1f137591bdf5b3d0787f9904b69f3245f5266ba363cff7d4e2de54.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-vaapi
ENV SELF="scripts.d/50-vaapi/50-libva.sh" STAGENAME="50-libva"
RUN --mount=src=scripts.d/50-vaapi/50-libva.sh,dst=/stage.sh --mount=src=.cache/downloads/50-libva_2cc27febb1d9727c4f44b3d2cf35341719a668c2b1c46cb5b2a3cb4ff8bf07ed.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-vaapi/99-finalize.sh" STAGENAME="99-finalize"
RUN --mount=src=scripts.d/50-vaapi/99-finalize.sh,dst=/stage.sh run_stage /stage.sh
FROM layer-45 AS layer-50-vidstab
ENV SELF="scripts.d/50-vidstab.sh" STAGENAME="50-vidstab"
RUN --mount=src=scripts.d/50-vidstab.sh,dst=/stage.sh --mount=src=.cache/downloads/50-vidstab_59b07ca229420fa43352a043a23b8e466b0d584fed9a0a5421d8d28a6e406c6d.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-vulkan
ENV SELF="scripts.d/50-vulkan/45-vulkan.sh" STAGENAME="45-vulkan"
RUN --mount=src=scripts.d/50-vulkan/45-vulkan.sh,dst=/stage.sh --mount=src=.cache/downloads/45-vulkan_1876a138322db3bb9fc5755e4616e9873afeb226748fc4a63575face62177420.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-vulkan/50-shaderc.sh" STAGENAME="50-shaderc"
RUN --mount=src=scripts.d/50-vulkan/50-shaderc.sh,dst=/stage.sh --mount=src=.cache/downloads/50-shaderc_42b9ec425924f6f633566aa5ebc0cb217d1dc1935eb29f4fa99dc10d3a31e11c.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-vulkan/55-spirv-cross.sh" STAGENAME="55-spirv-cross"
RUN --mount=src=scripts.d/50-vulkan/55-spirv-cross.sh,dst=/stage.sh --mount=src=.cache/downloads/55-spirv-cross_b3658ba596db4f759cd40f8029bfebcf1e761f7382ea2e22a52126ab22b8b555.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-vulkan/60-libplacebo.sh" STAGENAME="60-libplacebo"
RUN --mount=src=scripts.d/50-vulkan/60-libplacebo.sh,dst=/stage.sh --mount=src=.cache/downloads/60-libplacebo_98d7c5a48f1b185288dff5c6594331822975fee1cd5b0113140bc099cac3c579.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
ENV SELF="scripts.d/50-vulkan/99-enable.sh" STAGENAME="99-enable"
RUN --mount=src=scripts.d/50-vulkan/99-enable.sh,dst=/stage.sh run_stage /stage.sh
FROM layer-45 AS layer-50-vvenc
ENV SELF="scripts.d/50-vvenc.sh" STAGENAME="50-vvenc"
RUN --mount=src=scripts.d/50-vvenc.sh,dst=/stage.sh --mount=src=.cache/downloads/50-vvenc_cc0419d20b4299acb3da8690999762be154e14f661f7911f663cf3dd04e67ae8.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-x264
ENV SELF="scripts.d/50-x264.sh" STAGENAME="50-x264"
RUN --mount=src=scripts.d/50-x264.sh,dst=/stage.sh --mount=src=.cache/downloads/50-x264_a150eded7d4760c53b1f49ba5802f4a182afc2e0721af5936f9684600ae20a07.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-x265
ENV SELF="scripts.d/50-x265.sh" STAGENAME="50-x265"
RUN --mount=src=scripts.d/50-x265.sh,dst=/stage.sh --mount=src=.cache/downloads/50-x265_d9142b4531d006cd683ee5aeb7af13e887c644ec0901100313b8c694ae4b786b.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-xavs2
ENV SELF="scripts.d/50-xavs2.sh" STAGENAME="50-xavs2"
RUN --mount=src=scripts.d/50-xavs2.sh,dst=/stage.sh --mount=src=.cache/downloads/50-xavs2_983aacbab6d33318bf9c4742b41a1ed9d42afc12a0493ba97d07404a99734009.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-xvid
ENV SELF="scripts.d/50-xvid.sh" STAGENAME="50-xvid"
RUN --mount=src=scripts.d/50-xvid.sh,dst=/stage.sh --mount=src=.cache/downloads/50-xvid_e180ffb4bb79d24a145dbc9bb50c26233eabde100a4dd89af0d94b8c6e6135c4.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-zimg
ENV SELF="scripts.d/50-zimg.sh" STAGENAME="50-zimg"
RUN --mount=src=scripts.d/50-zimg.sh,dst=/stage.sh --mount=src=.cache/downloads/50-zimg_f5cc9139b601095018bf6776855afb5bdfb90e644656209f2848f8e3e9e6fad7.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50-zvbi
ENV SELF="scripts.d/50-zvbi.sh" STAGENAME="50-zvbi"
RUN --mount=src=scripts.d/50-zvbi.sh,dst=/stage.sh --mount=src=.cache/downloads/50-zvbi_0367a0cf4e6f979c2253bc0f1f865159b480cd6e7d0602662fd67ab4e14dc5e3.tar.xz,dst=/cache.tar.xz run_stage /stage.sh
FROM layer-45 AS layer-50
COPY --link --from=layer-50-amf $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-aom $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-aribb24 $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-avisynth $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-chromaprint $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-dav1d $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-davs2 $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-dvd $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-ffnvcodec $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-frei0r $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-gme $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-kvazaar $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libaribcaption $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libass $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libbluray $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libjxl $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libmp3lame $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libopus $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-librist $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libssh $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libtheora $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libvpx $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libwebp $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-libzmq $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-lilv $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-onevpl $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-openal $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-opencore-amr $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-openh264 $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-openjpeg $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-openmpt $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-rav1e $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-rubberband $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-schannel $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-sdl $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-soxr $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-srt $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-svtav1 $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-twolame $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-uavs3d $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-vaapi $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-vidstab $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-vulkan $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-vvenc $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-x264 $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-x265 $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-xavs2 $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-xvid $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-zimg $FFBUILD_PREFIX/. $FFBUILD_PREFIX
COPY --link --from=layer-50-zvbi $FFBUILD_PREFIX/. $FFBUILD_PREFIX
FROM layer-50 AS layer-99-rpath
FROM layer-50 AS layer-99
FROM base
COPY --from=layer-99 /opt/mingw/. /
COPY --link --from=layer-99 $FFBUILD_PREFIX/. $FFBUILD_PREFIX
ENV \
    FF_CONFIGURE="--enable-gpl --enable-version3 --disable-debug --enable-shared --disable-static --disable-w32threads --enable-pthreads --enable-iconv --enable-zlib --enable-libfreetype --enable-libfribidi --enable-gmp --enable-libxml2 --enable-lzma --enable-fontconfig --enable-libharfbuzz --enable-libvorbis --enable-opencl --disable-libpulse --enable-libvmaf --disable-libxcb --disable-xlib --enable-amf --enable-libaom --enable-libaribb24 --enable-avisynth --enable-chromaprint --enable-libdav1d --enable-libdavs2 --enable-libdvdread --enable-libdvdnav --disable-libfdk-aac --enable-ffnvcodec --enable-cuda-llvm --enable-frei0r --enable-libgme --enable-libkvazaar --enable-libaribcaption --enable-libass --enable-libbluray --enable-libjxl --enable-libmp3lame --enable-libopus --enable-librist --enable-libssh --enable-libtheora --enable-libvpx --enable-libwebp --enable-libzmq --enable-lv2 --enable-libvpl --enable-openal --enable-libopencore-amrnb --enable-libopencore-amrwb --enable-libopenh264 --enable-libopenjpeg --enable-libopenmpt --enable-librav1e --enable-librubberband --enable-schannel --enable-sdl2 --enable-libsoxr --enable-libsrt --enable-libsvtav1 --enable-libtwolame --enable-libuavs3d --disable-libdrm --enable-vaapi --enable-libvidstab --enable-vulkan --enable-libshaderc --enable-libplacebo --enable-libvvenc --enable-libx264 --enable-libx265 --enable-libxavs2 --enable-libxvid --enable-libzimg --enable-libzvbi" \
    FF_CFLAGS="-DLIBTWOLAME_STATIC" \
    FF_CXXFLAGS="" \
    FF_LDFLAGS="-pthread" \
    FF_LDEXEFLAGS="" \
    FF_LIBS="-lgomp"
